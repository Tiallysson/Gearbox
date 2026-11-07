using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gearbox.Core;
using Gearbox.Core.Settings;
using Gearbox.Core.Startup;
using Gearbox.Services;
using Microsoft.Extensions.Logging;

namespace Gearbox.ViewModels;

public sealed partial class GeneralViewModel : ObservableObject
{
    private readonly IStartupService _startup;
    private readonly ThemeService _theme;
    private readonly GearboxPaths _paths;
    private readonly UpdateService _updates;
    private readonly ISettingsService<GeneralSettings> _settings;
    private readonly ILogger<GeneralViewModel> _logger;
    private bool _syncing;
    private bool _attached;

    public GeneralViewModel(
        IStartupService startup,
        ThemeService theme,
        GearboxPaths paths,
        UpdateService updates,
        ISettingsService<GeneralSettings> settings,
        ILogger<GeneralViewModel> logger)
    {
        _startup = startup;
        _theme = theme;
        _paths = paths;
        _updates = updates;
        _settings = settings;
        _logger = logger;

        var general = settings.Load();
        _syncing = true;
        StartWithWindows = SafeIsStartupEnabled();
        ThemeIndex = (int)theme.Current;
        CheckForUpdates = general.CheckForUpdates;
        IncludePrereleases = general.IncludePrereleases || IsRunningPrerelease;
        _syncing = false;
        RefreshUpdateState();
    }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    /// <summary>0 = sistema, 1 = claro, 2 = escuro (mesma ordem de <see cref="AppTheme"/>).</summary>
    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    public partial bool CheckForUpdates { get; set; }

    [ObservableProperty]
    public partial bool IncludePrereleases { get; set; }

    public bool CanChangePrereleases => !IsRunningPrerelease;

    [ObservableProperty]
    public partial string UpdateStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanCheckForUpdates { get; set; }

    [ObservableProperty]
    public partial bool IsInstallVisible { get; set; }

    [ObservableProperty]
    public partial string InstallButtonText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsErrorOpen { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_syncing)
        {
            return;
        }

        try
        {
            _startup.SetEnabled(value);
            IsErrorOpen = false;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            _logger.LogError(ex, "Falha ao alterar a inicialização com o Windows");
            ErrorMessage = AppStrings.Get("StartupError");
            IsErrorOpen = true;

            _syncing = true;
            StartWithWindows = SafeIsStartupEnabled();
            _syncing = false;
        }
    }

    partial void OnThemeIndexChanged(int value)
    {
        if (!_syncing && Enum.IsDefined((AppTheme)value))
        {
            _theme.SetTheme((AppTheme)value);
        }
    }

    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _updates.StateChanged += OnUpdateStateChanged;
        RefreshUpdateState();
    }

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        _updates.StateChanged -= OnUpdateStateChanged;
    }

    partial void OnCheckForUpdatesChanged(bool value) => SaveGeneral(general => general.CheckForUpdates = value);

    partial void OnIncludePrereleasesChanged(bool value)
    {
        if (CanChangePrereleases)
        {
            SaveGeneral(general => general.IncludePrereleases = value);
        }
    }

    [RelayCommand]
    private Task CheckForUpdatesNowAsync() => _updates.CheckNowAsync();

    [RelayCommand]
    private Task InstallUpdateAsync() => _updates.InstallAsync();

    [RelayCommand]
    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        Process.Start(new ProcessStartInfo { FileName = _paths.LogsDirectory, UseShellExecute = true });
    }

    private bool IsRunningPrerelease => _updates.CurrentVersion?.IsPrerelease == true;

    private void SaveGeneral(Action<GeneralSettings> change)
    {
        if (_syncing)
        {
            return;
        }

        var general = _settings.Load();
        change(general);
        _settings.Save(general);
    }

    private void OnUpdateStateChanged(object? sender, EventArgs e) => RefreshUpdateState();

    private void RefreshUpdateState()
    {
        var current = _updates.CurrentVersion;
        var candidate = _updates.Candidate?.Release.Version;
        UpdateStatusText = current is null
            ? AppStrings.Get("UpdateUnavailable")
            : _updates.Status switch
            {
                UpdateStatus.Checking => AppStrings.Get("UpdateChecking"),
                UpdateStatus.UpToDate => AppStrings.Format("UpdateUpToDateFormat", current),
                UpdateStatus.Available => AppStrings.Format("UpdateAvailableFormat", candidate!, current),
                UpdateStatus.Downloading => AppStrings.Format("UpdateDownloadingFormat", candidate!),
                UpdateStatus.Failed => AppStrings.Get("UpdateFailed"),
                _ => AppStrings.Format("UpdateCurrentFormat", current),
            };
        CanCheckForUpdates = current is not null && !_updates.IsBusy;
        IsInstallVisible = candidate is not null && !_updates.IsBusy;
        InstallButtonText = candidate is null ? string.Empty : AppStrings.Format("UpdateInstallFormat", candidate);
    }

    private bool SafeIsStartupEnabled()
    {
        try
        {
            return _startup.IsEnabled;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            _logger.LogWarning(ex, "Não foi possível ler a inicialização com o Windows");
            return false;
        }
    }
}
