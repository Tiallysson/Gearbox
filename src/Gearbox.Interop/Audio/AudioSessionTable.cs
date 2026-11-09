using System.Runtime.InteropServices;
using Gearbox.Interop.Processes;
using Microsoft.Extensions.Logging;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;
using Windows.Win32.Media.Audio.Endpoints;
using Windows.Win32.System.Com;

namespace Gearbox.Interop.Audio;

public sealed record AudioSessionInfo(
    string InstanceId,
    uint ProcessId,
    string? ExecutablePath,
    string? DisplayName,
    bool IsSystemSounds,
    bool IsActive,
    bool IsExpired,
    float Volume,
    bool IsMuted,
    string DeviceId);

public sealed record AudioSessionScan(List<AudioSessionInfo> Sessions, HashSet<string> UnreadDevices);

public sealed class AudioSessionTable : IDisposable
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, IAudioSessionManager2> _managers = [];
    private readonly Dictionary<string, Entry> _sessions = [];
    private IMMDeviceEnumerator? _enumerator;

    public AudioSessionTable(ILogger logger)
    {
        _logger = logger;
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
    }

    public AudioSessionScan Scan()
    {
        var result = new List<AudioSessionInfo>();
        var unread = new HashSet<string>(StringComparer.Ordinal);
        if (_enumerator is null)
        {
            return new AudioSessionScan(result, unread);
        }

        var seenSessions = new HashSet<string>();
        var devices = ActiveRenderDeviceIds(_enumerator);
        foreach (var deviceId in devices)
        {
            if (GetManager(deviceId) is not { } manager || !ScanDevice(deviceId, manager, seenSessions, result))
            {
                unread.Add(deviceId);
            }
        }

        foreach (var gone in _sessions.Where(pair => !seenSessions.Contains(pair.Key) && !unread.Contains(pair.Value.DeviceId)).Select(pair => pair.Key).ToList())
        {
            Marshal.ReleaseComObject(_sessions[gone].Session);
            _sessions.Remove(gone);
        }

        foreach (var gone in _managers.Keys.Where(id => !devices.Contains(id)).ToList())
        {
            Marshal.ReleaseComObject(_managers[gone]);
            _managers.Remove(gone);
        }

        return new AudioSessionScan(result, unread);
    }

    public float ReadPeak(string instanceId)
    {
        if (!_sessions.TryGetValue(instanceId, out var entry) || entry.Session is not IAudioMeterInformation meter)
        {
            return 0;
        }

        try
        {
            meter.GetPeakValue(out var peak);
            return float.IsFinite(peak) ? Math.Clamp(peak, 0f, 1f) : 0;
        }
        catch (COMException)
        {
            return 0;
        }
    }

    public unsafe bool TrySetVolume(string instanceId, float level)
    {
        if (!float.IsFinite(level) || !_sessions.TryGetValue(instanceId, out var entry) || entry.Session is not ISimpleAudioVolume volume)
        {
            return false;
        }

        try
        {
            volume.SetMasterVolume(Math.Clamp(level, 0f, 1f), null);
            return true;
        }
        catch (COMException ex)
        {
            _logger.LogDebug(ex, "Não foi possível mudar o volume da sessão {Session}", instanceId);
            return false;
        }
    }

    public unsafe bool TrySetMuted(string instanceId, bool muted)
    {
        if (!_sessions.TryGetValue(instanceId, out var entry) || entry.Session is not ISimpleAudioVolume volume)
        {
            return false;
        }

        try
        {
            volume.SetMute(muted, null);
            return true;
        }
        catch (COMException ex)
        {
            _logger.LogDebug(ex, "Não foi possível mudar o mudo da sessão {Session}", instanceId);
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var entry in _sessions.Values)
        {
            Marshal.ReleaseComObject(entry.Session);
        }

        foreach (var manager in _managers.Values)
        {
            Marshal.ReleaseComObject(manager);
        }

        _sessions.Clear();
        _managers.Clear();
        if (_enumerator is not null)
        {
            Marshal.ReleaseComObject(_enumerator);
            _enumerator = null;
        }
    }

    private bool ScanDevice(string deviceId, IAudioSessionManager2 manager, HashSet<string> seen, List<AudioSessionInfo> result)
    {
        IAudioSessionEnumerator sessions;
        try
        {
            sessions = manager.GetSessionEnumerator();
        }
        catch (COMException ex)
        {
            _logger.LogDebug(ex, "Não foi possível listar as sessões de áudio de {Device}", deviceId);
            Marshal.ReleaseComObject(manager);
            _managers.Remove(deviceId);
            return false;
        }

        try
        {
            var complete = true;
            sessions.GetCount(out var count);
            for (var i = 0; i < count; i++)
            {
                sessions.GetSession(i, out var control);
                if (TryRead(control, deviceId) is not { } info)
                {
                    complete = false;
                }
                else if (seen.Add(info.InstanceId))
                {
                    result.Add(info);
                }
            }

            return complete;
        }
        catch (COMException ex)
        {
            _logger.LogDebug(ex, "Lista de sessões de áudio interrompida em {Device}", deviceId);
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(sessions);
        }
    }

    private unsafe AudioSessionInfo? TryRead(IAudioSessionControl control, string deviceId)
    {
        string? instanceId = null;
        try
        {
            var session = (IAudioSessionControl2)control;
            PWSTR id;
            session.GetSessionInstanceIdentifier(&id);
            instanceId = TakeString(id);
            if (instanceId is null)
            {
                Marshal.ReleaseComObject(control);
                return null;
            }

            if (_sessions.TryGetValue(instanceId, out var cached))
            {
                if (!ReferenceEquals(cached.Session, control))
                {
                    Marshal.ReleaseComObject(cached.Session);
                    _sessions[instanceId] = cached with { Session = control };
                }
                else
                {
                    Marshal.ReleaseComObject(control);
                }
            }
            else
            {
                session.GetProcessId(out var processId);
                var isSystem = session.IsSystemSoundsSession() == HRESULT.S_OK;
                cached = new Entry(control, processId, isSystem ? null : ProcessImage.TryGetPath(processId), isSystem, deviceId);
                _sessions[instanceId] = cached;
            }

            AudioSessionState state;
            session.GetState(&state);
            PWSTR name;
            session.GetDisplayName(&name);
            var volume = (ISimpleAudioVolume)control;
            volume.GetMasterVolume(out var level);
            BOOL muted;
            volume.GetMute(&muted);

            return new AudioSessionInfo(
                instanceId,
                cached.ProcessId,
                cached.ExecutablePath,
                TakeString(name),
                cached.IsSystemSounds,
                state == AudioSessionState.AudioSessionStateActive,
                state == AudioSessionState.AudioSessionStateExpired,
                float.IsFinite(level) ? Math.Clamp(level, 0f, 1f) : 1f,
                muted,
                deviceId);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            _logger.LogDebug(ex, "Sessão de áudio ignorada em {Device}", deviceId);
            if (instanceId is not null && _sessions.Remove(instanceId, out var stale))
            {
                Marshal.ReleaseComObject(stale.Session);
            }
            else
            {
                Marshal.ReleaseComObject(control);
            }

            return null;
        }
    }

    private unsafe IAudioSessionManager2? GetManager(string deviceId)
    {
        if (_managers.TryGetValue(deviceId, out var manager))
        {
            return manager;
        }

        IMMDevice? device = null;
        try
        {
            fixed (char* id = deviceId)
            {
                _enumerator!.GetDevice(id, out device);
            }

            var iid = typeof(IAudioSessionManager2).GUID;
            device.Activate(&iid, CLSCTX.CLSCTX_ALL, null, out var activated);
            manager = (IAudioSessionManager2)activated;
            _managers[deviceId] = manager;
            return manager;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            _logger.LogDebug(ex, "Sem gerenciador de sessões de áudio para {Device}", deviceId);
            return null;
        }
        finally
        {
            if (device is not null)
            {
                Marshal.ReleaseComObject(device);
            }
        }
    }

    private unsafe HashSet<string> ActiveRenderDeviceIds(IMMDeviceEnumerator enumerator)
    {
        var ids = new HashSet<string>();
        try
        {
            enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE.DEVICE_STATE_ACTIVE, out var collection);
            try
            {
                collection.GetCount(out var count);
                for (uint i = 0; i < count; i++)
                {
                    collection.Item(i, out var device);
                    try
                    {
                        PWSTR id;
                        device.GetId(&id);
                        if (TakeString(id) is { } value)
                        {
                            ids.Add(value);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(collection);
            }
        }
        catch (COMException ex)
        {
            _logger.LogDebug(ex, "Não foi possível listar as saídas de áudio");
        }

        return ids;
    }

    private static unsafe string? TakeString(PWSTR value)
    {
        if (value.Value is null)
        {
            return null;
        }

        try
        {
            var text = value.ToString();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)value.Value);
        }
    }

    private sealed record Entry(object Session, uint ProcessId, string? ExecutablePath, bool IsSystemSounds, string DeviceId);
}
