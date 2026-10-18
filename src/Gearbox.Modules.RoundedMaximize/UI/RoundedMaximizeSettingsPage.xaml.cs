using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Gearbox.Modules.RoundedMaximize.UI;

public sealed partial class RoundedMaximizeSettingsPage : Page
{
    public RoundedMaximizeSettingsPage(RoundedMaximizeSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Unloaded += (_, _) => ViewModel.Flush();
    }

    public RoundedMaximizeSettingsViewModel ViewModel { get; }

    private void OnRemoveExclusionClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string processName })
        {
            ViewModel.RemoveExclusionCommand.Execute(processName);
        }
    }

    private void OnNewExclusionKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.AddExclusionCommand.CanExecute(null))
        {
            ViewModel.AddExclusionCommand.Execute(null);
            e.Handled = true;
        }
    }
}
