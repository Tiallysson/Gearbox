using Microsoft.UI.Xaml.Controls;

namespace Gearbox.Modules.Notch.UI;

public sealed partial class NotchSettingsPage : Page
{
    public NotchSettingsPage(NotchSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Unloaded += (_, _) => ViewModel.Flush();
    }

    public NotchSettingsViewModel ViewModel { get; }
}
