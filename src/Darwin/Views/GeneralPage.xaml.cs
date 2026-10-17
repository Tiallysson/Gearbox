using Darwin.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Darwin.Views;

public sealed partial class GeneralPage : Page
{
    public GeneralPage(GeneralViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public GeneralViewModel ViewModel { get; }
}
