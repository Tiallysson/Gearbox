using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gearbox.Core;
using Gearbox.Core.Startup;
using Gearbox.Services;
using Microsoft.Extensions.Logging;

namespace Gearbox.ViewModels;

public sealed partial class GeneralViewModel : ObservableObject
{
    private readonly IStartupService _startup;
    private readonly ThemeService _theme;
    private readonly GearboxPaths _paths;
    private readonly ILogger<GeneralViewModel> _logger;
    private bool _syncing;

    public GeneralViewModel(IStartupService startup, ThemeService theme, GearboxPaths paths, ILogger<GeneralViewModel> logger)
    {
        _startup = startup;
        _theme = theme;
        _paths = paths;
        _logger = logger;

        _syncing = true;
        StartWithWindows = SafeIsStartupEnabled();
        ThemeIndex = (int)theme.Current;
        _syncing = false;
    }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    /// <summary>0 = sistema, 1 = claro, 2 = escuro (mesma ordem de <see cref="AppTheme"/>).</summary>
    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

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

    [RelayCommand]
    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        Process.Start(new ProcessStartInfo { FileName = _paths.LogsDirectory, UseShellExecute = true });
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
