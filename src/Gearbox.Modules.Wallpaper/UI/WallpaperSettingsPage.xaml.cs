using Microsoft.UI.Xaml.Controls;

namespace Gearbox.Modules.Wallpaper.UI;

public sealed partial class WallpaperSettingsPage : Page
{
    public WallpaperSettingsPage(WallpaperSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // O seletor de arquivos precisa saber de qual janela ele é filho.
        Loaded += (_, _) =>
        {
            if (XamlRoot?.ContentIslandEnvironment is { } environment)
            {
                ViewModel.OwnerWindow = environment.AppWindowId;
            }
        };
        Unloaded += (_, _) => ViewModel.Flush();
    }

    public WallpaperSettingsViewModel ViewModel { get; }
}
