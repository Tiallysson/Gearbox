using Darwin.Core.Settings;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Darwin.Services;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Configurações do casco, em <c>settings\general.json</c>.</summary>
public sealed class GeneralSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
}

/// <summary>
/// Tema da janela principal. No WinUI 3 o tema do <c>Application</c> só pode ser definido no construtor,
/// então ele é aplicado no elemento raiz da janela (o Mica e a barra de título acompanham).
/// </summary>
public sealed class ThemeService(ISettingsService<GeneralSettings> settings)
{
    private Window? _window;

    public AppTheme Current { get; private set; }

    public void Initialize() => Current = settings.Load().Theme;

    public void Attach(Window window)
    {
        _window = window;
        Apply();
    }

    public void SetTheme(AppTheme theme)
    {
        if (theme == Current)
        {
            return;
        }

        Current = theme;
        var general = settings.Load();
        general.Theme = theme;
        settings.Save(general);
        Apply();
    }

    private void Apply()
    {
        if (_window is null)
        {
            return;
        }

        if (_window.Content is FrameworkElement root)
        {
            root.RequestedTheme = Current switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }

        _window.AppWindow.TitleBar.PreferredTheme = Current switch
        {
            AppTheme.Light => TitleBarTheme.Light,
            AppTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }
}
