using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Gearbox.Modules.Notch.UI;

public sealed partial class NotchSettingsPage : Page
{
    public NotchSettingsPage(NotchSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Unloaded += (_, _) =>
        {
            ViewModel.Flush();
            ViewModel.Detach();
        };
    }

    public NotchSettingsViewModel ViewModel { get; }

    private void OnAddLinkClick(object sender, RoutedEventArgs e) => ViewModel.AddLink();

    private void OnLinkMoveUpClick(object sender, RoutedEventArgs e) => WithLinkId(sender, id => ViewModel.MoveLink(id, -1));

    private void OnLinkMoveDownClick(object sender, RoutedEventArgs e) => WithLinkId(sender, id => ViewModel.MoveLink(id, 1));

    private void OnLinkRemoveClick(object sender, RoutedEventArgs e) => WithLinkId(sender, ViewModel.RemoveLink);

    private static void WithLinkId(object sender, Action<string> action)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            action(id);
        }
    }

    private void OnCustomColorApply(object sender, RoutedEventArgs e)
    {
        var color = CustomColorPicker.Color;
        ViewModel.UseCustomColor(new TintColor(color.R, color.G, color.B));
        CustomColorFlyout.Hide();
    }
}
