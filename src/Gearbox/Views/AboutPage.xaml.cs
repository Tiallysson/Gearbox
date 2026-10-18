using Gearbox.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Gearbox.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage(AboutViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public AboutViewModel ViewModel { get; }
}
