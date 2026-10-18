using Gearbox.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Gearbox.Views;

public sealed partial class GeneralPage : Page
{
    public GeneralPage(GeneralViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public GeneralViewModel ViewModel { get; }
}
