using Darwin.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Darwin.Views;

/// <summary>Moldura do casco em volta da página de configurações de um módulo.</summary>
public sealed partial class ModulePage : Page
{
    public ModulePage(ModulePageViewModel viewModel, UIElement settingsContent)
    {
        ViewModel = viewModel;
        InitializeComponent();
        SettingsHost.Content = settingsContent;
    }

    public ModulePageViewModel ViewModel { get; }
}
