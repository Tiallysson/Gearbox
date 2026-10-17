using Darwin.Core.Modules;
using Darwin.Interop;
using Darwin.Services;
using Darwin.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Darwin.Views;

public sealed partial class MainWindow : Window
{
    private const string GeneralTag = "darwin:general";
    private const string AboutTag = "darwin:about";

    private readonly IServiceProvider _services;
    private readonly ModuleManager _modules;
    private readonly ILogger<MainWindow> _logger;
    private ModulePageViewModel? _currentModuleViewModel;

    public MainWindow(IServiceProvider services, ModuleManager modules, ThemeService theme, ILogger<MainWindow> logger)
    {
        _services = services;
        _modules = modules;
        _logger = logger;

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Darwin.ico"));
        AppWindow.Closing += OnClosing;

        BuildNavigation();
        theme.Attach(this);
        SizeAndCenter(1040, 760);

        NavView.SelectedItem = NavView.MenuItems[0];
    }

    /// <summary>Quando falso (padrão), fechar a janela apenas a esconde; o app continua na bandeja.</summary>
    public bool AllowClose { get; set; }

    public void ShowAndActivate()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        AppWindow.Show();
        Activate();
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!AllowClose)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

    /// <summary>"Geral" no topo, um item por módulo registrado e "Sobre" no rodapé. Nada de módulo fixo aqui.</summary>
    private void BuildNavigation()
    {
        NavView.MenuItems.Add(CreateItem(AppStrings.Get("NavGeneral"), "", GeneralTag));

        if (_modules.Modules.Count > 0)
        {
            NavView.MenuItems.Add(new NavigationViewItemHeader { Content = AppStrings.Get("NavModulesHeader") });
            foreach (var module in _modules.Modules.OrderBy(m => m.DisplayName, StringComparer.CurrentCulture))
            {
                NavView.MenuItems.Add(CreateItem(module.DisplayName, module.IconGlyph, module.Id));
            }
        }

        NavView.FooterMenuItems.Add(CreateItem(AppStrings.Get("NavAbout"), "", AboutTag));
    }

    private static NavigationViewItem CreateItem(string text, string glyph, string tag) => new()
    {
        Content = text,
        Icon = new FontIcon { Glyph = glyph },
        Tag = tag,
    };

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            Navigate(tag);
        }
    }

    private void Navigate(string tag)
    {
        _currentModuleViewModel?.Dispose();
        _currentModuleViewModel = null;

        ContentFrame.Content = tag switch
        {
            GeneralTag => _services.GetRequiredService<GeneralPage>(),
            AboutTag => _services.GetRequiredService<AboutPage>(),
            _ => CreateModulePage(tag),
        };
    }

    private ModulePage? CreateModulePage(string moduleId)
    {
        var module = _modules.Find(moduleId);
        if (module is null)
        {
            return null;
        }

        UIElement content;
        try
        {
            content = (UIElement)ActivatorUtilities.GetServiceOrCreateInstance(_services, module.SettingsPageType);
        }
        catch (Exception ex)
        {
            // Uma página de módulo quebrada não pode derrubar a janela.
            _logger.LogError(ex, "Falha ao criar a página de configurações do módulo {ModuleId}", module.Id);
            content = new TextBlock
            {
                Text = AppStrings.Format("ModulePageLoadErrorFormat", ex.Message),
                TextWrapping = TextWrapping.Wrap,
            };
        }

        _currentModuleViewModel = new ModulePageViewModel(module, _modules);
        return new ModulePage(_currentModuleViewModel, content);
    }

    private void SizeAndCenter(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = NativeWindow.GetDpiScale(hwnd);
        var size = new SizeInt32((int)(width * scale), (int)(height * scale));

        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        size.Width = Math.Min(size.Width, workArea.Width);
        size.Height = Math.Min(size.Height, workArea.Height);

        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + ((workArea.Width - size.Width) / 2),
            workArea.Y + ((workArea.Height - size.Height) / 2),
            size.Width,
            size.Height));
    }
}
