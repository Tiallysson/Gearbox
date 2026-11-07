using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Gearbox.Core.Settings;
using Gearbox.Core.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Gearbox.Services;

public enum UpdateStatus
{
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,
    Failed,
}

public sealed class UpdateService(
    IReleaseSource releases,
    UpdateDownloader downloader,
    ISettingsService<GeneralSettings> settings,
    ILogger<UpdateService> logger) : IDisposable
{
    private const string SilentInstallArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /relaunch=1";

    private static readonly TimeSpan s_firstCheckDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan s_checkInterval = TimeSpan.FromHours(24);

    private DispatcherQueueTimer? _timer;
    private AppVersion? _announcedVersion;

    public event EventHandler? StateChanged;

    public event EventHandler<UpdateCandidate>? UpdateFound;

    public event EventHandler? ShutdownRequested;

    public AppVersion? CurrentVersion { get; } = AppVersion.TryParse(AppInfo.Version, out var version) ? version : null;

    public UpdateStatus Status { get; private set; }

    public UpdateCandidate? Candidate { get; private set; }

    public bool IsBusy => Status is UpdateStatus.Checking or UpdateStatus.Downloading;

    public void Start()
    {
        if (_timer is not null || CurrentVersion is null)
        {
            return;
        }

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = s_firstCheckDelay;
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    public async Task CheckNowAsync()
    {
        if (IsBusy || CurrentVersion is not { } current)
        {
            return;
        }

        SetState(UpdateStatus.Checking, Candidate);
        try
        {
            var available = await releases.GetReleasesAsync(CancellationToken.None);
            var includePrereleases = UpdateSelector.IncludesPrereleases(settings.Load().IncludePrereleases, current);
            var candidate = UpdateSelector.Select(current, available, RuntimeInformation.ProcessArchitecture, includePrereleases);
            SetState(candidate is null ? UpdateStatus.UpToDate : UpdateStatus.Available, candidate);
            AnnounceOnce(candidate);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Não foi possível verificar atualizações");
            SetState(UpdateStatus.Failed, Candidate);
        }
    }

    public async Task InstallAsync()
    {
        if (IsBusy || Candidate is not { } candidate)
        {
            return;
        }

        SetState(UpdateStatus.Downloading, candidate);
        try
        {
            var installer = await downloader.DownloadAsync(candidate.Installer, CancellationToken.None);
            logger.LogInformation("Iniciando o instalador de {Version}", candidate.Release.Version);
            Process.Start(new ProcessStartInfo(installer, SilentInstallArguments) { UseShellExecute = true });
            ShutdownRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or OperationCanceledException or Win32Exception)
        {
            logger.LogError(ex, "Falha ao instalar a atualização {Version}", candidate.Release.Version);
            SetState(UpdateStatus.Failed, candidate);
        }
    }

    public void Dispose()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Tick -= OnTimerTick;
        _timer.Stop();
        _timer = null;
    }

    private async void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Interval = s_checkInterval;
        if (settings.Load().CheckForUpdates)
        {
            await CheckNowAsync();
        }
    }

    private void AnnounceOnce(UpdateCandidate? candidate)
    {
        if (candidate is null || candidate.Release.Version == _announcedVersion)
        {
            return;
        }

        _announcedVersion = candidate.Release.Version;
        UpdateFound?.Invoke(this, candidate);
    }

    private void SetState(UpdateStatus status, UpdateCandidate? candidate)
    {
        Status = status;
        Candidate = candidate;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
