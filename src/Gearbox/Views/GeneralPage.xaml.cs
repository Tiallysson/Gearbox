using Gearbox.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Gearbox.Views;

public sealed partial class GeneralPage : Page
{
    public GeneralPage(GeneralViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += (_, _) => ViewModel.Attach();
        Unloaded += (_, _) => ViewModel.Detach();
    }

    public GeneralViewModel ViewModel { get; }
}
