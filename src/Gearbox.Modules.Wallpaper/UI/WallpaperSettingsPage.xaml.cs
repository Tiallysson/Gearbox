using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

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

            if (XamlRoot is { } root)
            {
                ViewModel.IsPageVisible = root.IsHostVisible;
                root.Changed += OnXamlRootChanged;
            }
        };
        Unloaded += (_, _) =>
        {
            if (XamlRoot is { } root)
            {
                root.Changed -= OnXamlRootChanged;
            }

            ViewModel.Flush();
        };
    }

    public WallpaperSettingsViewModel ViewModel { get; }

    private bool _dialogOpen;

    private void OnFilterChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) =>
        ViewModel.FilterIndex = sender.Items.IndexOf(sender.SelectedItem);

    private async void OnCardClick(object sender, ItemClickEventArgs e)
    {
        if (_dialogOpen || e.ClickedItem is not GalleryCardViewModel card)
        {
            return;
        }

        _dialogOpen = true;
        card.IsHovered = false;
        ViewModel.SelectedItem = card;
        try
        {
            DetailDialog.XamlRoot = XamlRoot;
            await DetailDialog.ShowAsync();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
        }
        finally
        {
            ViewModel.SelectedItem = null;
            _dialogOpen = false;
        }
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => ViewModel.IsPageVisible = sender.IsHostVisible;

    private void OnActivateClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedCard is { } card)
        {
            ViewModel.Activate(card);
            DetailDialog.Hide();
        }
    }

    private void OnDeactivateClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Deactivate();
        DetailDialog.Hide();
    }

    private void OnCardPointerEntered(object sender, PointerRoutedEventArgs e) => SetHovered(sender, true);

    private void OnCardPointerExited(object sender, PointerRoutedEventArgs e) => SetHovered(sender, false);

    private static void SetHovered(object sender, bool hovered)
    {
        if (sender is FrameworkElement { DataContext: GalleryCardViewModel card })
        {
            card.IsHovered = hovered;
        }
    }
}
