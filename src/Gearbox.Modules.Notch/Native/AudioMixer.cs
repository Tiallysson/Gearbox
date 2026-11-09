using System.Diagnostics;
using Gearbox.Core.Settings;
using Gearbox.Interop.Audio;
using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Gearbox.Modules.Notch.Native;

internal sealed class AudioMixer : IDisposable
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MeterInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly ISettingsService<MixerSettings> _settings;
    private readonly ILogger _logger;
    private readonly AudioSessionTable _table;
    private readonly DispatcherQueueTimer _scanTimer;
    private readonly DispatcherQueueTimer _meterTimer;
    private readonly DispatcherQueueTimer _saveTimer;
    private readonly HashSet<string> _volumeKeys = [];
    private readonly HashSet<string> _routeKeys = [];
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _peaks = new(StringComparer.Ordinal);
    private Dictionary<string, MixerAppPreference> _preferences;
    private Dictionary<string, string> _known = new(StringComparer.Ordinal);
    private List<AudioSessionInfo> _sessions = [];
    private bool _disposed;

    public AudioMixer(DispatcherQueue dispatcher, ISettingsService<MixerSettings> settings, ILogger logger, Action<Action> guard)
    {
        _settings = settings;
        _logger = logger;
        _table = new AudioSessionTable(logger);
        try
        {
            _preferences = MixerMemory.Valid(settings.Load().Apps);
            _scanTimer = CreateTimer(dispatcher, ScanInterval, repeating: true, () => guard(Scan));
            _meterTimer = CreateTimer(dispatcher, MeterInterval, repeating: true, () => guard(ReadPeaks));
            _saveTimer = CreateTimer(dispatcher, SaveDelay, repeating: false, () => guard(Save));
            Scan();
            _scanTimer.Start();
        }
        catch
        {
            _table.Dispose();
            throw;
        }
    }

    public event EventHandler? AppsChanged;

    public event EventHandler? PeaksChanged;

    public IReadOnlyList<MixerApp> Apps { get; private set; } = [];

    public IReadOnlyDictionary<string, double> Peaks => _peaks;

    public void SetMeterActive(bool active)
    {
        if (_disposed || active == _meterTimer.IsRunning)
        {
            return;
        }

        if (active)
        {
            _meterTimer.Start();
            return;
        }

        _meterTimer.Stop();
        if (_peaks.Count > 0)
        {
            _peaks.Clear();
            PeaksChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetVolume(string key, double percent)
    {
        if (_disposed || !double.IsFinite(percent))
        {
            return;
        }

        var level = (float)Math.Clamp(percent / 100.0, 0, 1);
        var applied = false;
        foreach (var session in MixerApps.SessionsOf(_sessions, key))
        {
            applied |= _table.TrySetVolume(session.InstanceId, level);
        }

        if (!applied)
        {
            return;
        }

        _volumeKeys.Add(key);
        Remember(key, preference => preference with { Volume = level });
        Publish(app => app.Key == key ? app with { Volume = level } : app);
    }

    public void ToggleMute(string key)
    {
        if (_disposed || Apps.FirstOrDefault(app => app.Key == key) is not { } target)
        {
            return;
        }

        var muted = !target.IsMuted;
        var applied = false;
        foreach (var session in MixerApps.SessionsOf(_sessions, key))
        {
            applied |= _table.TrySetMuted(session.InstanceId, muted);
        }

        if (!applied)
        {
            return;
        }

        _volumeKeys.Add(key);
        Remember(key, preference => preference with { Muted = muted });
        Publish(app => app.Key == key ? app with { IsMuted = muted } : app);
    }

    public void SetOutput(string key, string? deviceId)
    {
        if (_disposed)
        {
            return;
        }

        var routes = MixerMemory.RouteApp(_sessions, key, deviceId);
        var applied = false;
        foreach (var route in routes)
        {
            applied |= AppAudioRouting.TrySetOutput(route.ProcessId, route.DeviceId, _logger);
        }

        if (!applied)
        {
            return;
        }

        _routeKeys.Add(key);
        Remember(key, preference => preference with { DeviceId = deviceId });
        Publish(app => app.Key == key ? app with { OutputDeviceId = deviceId } : app);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scanTimer.Stop();
        _meterTimer.Stop();
        if (_saveTimer.IsRunning)
        {
            _saveTimer.Stop();
            Save();
        }

        try
        {
            RestoreTouched();
        }
        finally
        {
            _table.Dispose();
        }
    }

    private void RestoreTouched()
    {
        if (_volumeKeys.Count == 0 && _routeKeys.Count == 0)
        {
            return;
        }

        var plan = MixerMemory.Reset(_table.Scan().Sessions, _volumeKeys, _routeKeys);
        foreach (var instanceId in plan.RestoreSessions)
        {
            _table.TrySetVolume(instanceId, 1f);
            _table.TrySetMuted(instanceId, false);
        }

        foreach (var processId in plan.UnrouteProcesses)
        {
            AppAudioRouting.TrySetOutput(processId, null, _logger);
        }

        _logger.LogInformation(
            "Mixer: {Sessions} sessões voltaram a 100% e {Processes} apps voltaram à saída padrão",
            plan.RestoreSessions.Count,
            plan.UnrouteProcesses.Count);
    }

    private void Scan()
    {
        if (_disposed)
        {
            return;
        }

        var scan = _table.Scan();
        var sessions = scan.Sessions;
        var fresh = MixerMemory.Fresh(sessions, _known);
        _known = MixerMemory.Known(_known, sessions, scan.UnreadDevices);
        if (fresh.Count > 0 && Reapply(fresh))
        {
            sessions = _table.Scan().Sessions;
        }

        _sessions = sessions;
        var apps = MixerApps.Open(sessions, NameOf, _preferences);
        if (!apps.SequenceEqual(Apps))
        {
            Apps = apps;
            AppsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool Reapply(List<AudioSessionInfo> fresh)
    {
        var changed = false;
        foreach (var restore in MixerMemory.VolumePlan(fresh, _preferences))
        {
            var key = MixerApps.KeyFor(fresh.First(session => session.InstanceId == restore.InstanceId));
            if (restore.Volume is { } volume && _table.TrySetVolume(restore.InstanceId, volume))
            {
                _volumeKeys.Add(key);
                changed = true;
            }

            if (restore.Muted is { } muted && _table.TrySetMuted(restore.InstanceId, muted))
            {
                _volumeKeys.Add(key);
                changed = true;
            }
        }

        if (!_preferences.Values.Any(preference => preference.DeviceId is not null))
        {
            return changed;
        }

        var available = AudioEndpoints.GetOutputDevices(_logger).Select(device => device.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var route in MixerMemory.RoutePlan(fresh, _preferences, available))
        {
            if (AppAudioRouting.TrySetOutput(route.ProcessId, route.DeviceId, _logger))
            {
                var key = MixerApps.KeyFor(fresh.First(session => session.ProcessId == route.ProcessId));
                _routeKeys.Add(key);
                changed = true;
            }
        }

        return changed;
    }

    private void ReadPeaks()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var app in Apps)
        {
            var peak = 0f;
            foreach (var session in MixerApps.SessionsOf(_sessions, app.Key))
            {
                if (session.IsActive)
                {
                    peak = Math.Max(peak, _table.ReadPeak(session.InstanceId));
                }
            }

            _peaks[app.Key] = MixerApps.Meter(_peaks.GetValueOrDefault(app.Key), peak);
        }

        foreach (var gone in _peaks.Keys.Where(key => Apps.All(app => app.Key != key)).ToList())
        {
            _peaks.Remove(gone);
        }

        PeaksChanged?.Invoke(this, EventArgs.Empty);
    }

    private string NameOf(AudioSessionInfo session)
    {
        if (session.IsSystemSounds)
        {
            return ModuleStrings.Get("SystemSoundsName");
        }

        if (session.ExecutablePath is not { } path)
        {
            return MixerApps.FallbackName(session);
        }

        if (!_names.TryGetValue(path, out var name))
        {
            name = ReadDescription(path) ?? MixerApps.FallbackName(session);
            _names[path] = name;
        }

        return name;
    }

    private string? ReadDescription(string path)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
            return string.IsNullOrEmpty(description) ? null : description;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogDebug(ex, "Sem descrição para {Path}", path);
            return null;
        }
    }

    private void Publish(Func<MixerApp, MixerApp> change)
    {
        Apps = Apps.Select(change).ToList();
        AppsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Remember(string key, Func<MixerAppPreference, MixerAppPreference> change)
    {
        if (!MixerApps.IsPersistable(key))
        {
            return;
        }

        _preferences = MixerMemory.With(_preferences, key, change);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Save()
    {
        try
        {
            _settings.Save(new MixerSettings { Apps = _preferences.ToDictionary(pair => pair.Key, MixerAppPreference? (pair) => pair.Value, StringComparer.Ordinal) });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Não foi possível salvar o mixer do Notch");
        }
    }

    private static DispatcherQueueTimer CreateTimer(DispatcherQueue dispatcher, TimeSpan interval, bool repeating, Action tick)
    {
        var timer = dispatcher.CreateTimer();
        timer.Interval = interval;
        timer.IsRepeating = repeating;
        timer.Tick += (_, _) => tick();
        return timer;
    }
}
