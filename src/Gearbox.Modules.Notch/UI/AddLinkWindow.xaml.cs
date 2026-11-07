using System.Runtime.InteropServices;
using Gearbox.Interop.Shell;
using Gearbox.Interop.Windowing;
using Gearbox.Modules.Notch.Logic;
using Gearbox.Modules.Notch.Native;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace Gearbox.Modules.Notch.UI;

internal sealed partial class AddLinkWindow : Window
{
    private const int WidthDips = 480;
    private const int HeightDips = 600;

    private readonly LinkCatalog _catalog;
    private readonly ILogger _logger;
    private List<AppChoice>? _apps;

    public AddLinkWindow(LinkCatalog catalog, ILogger logger)
    {
        _catalog = catalog;
        _logger = logger;
        InitializeComponent();
        Title = ModuleStrings.Get("AddLinkTitle");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Gearbox.ico"));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        PlaceOnPrimaryDisplay();
        UpdateAddButton();
        Activated += OnFirstActivated;
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        AddressBox.Focus(FocusState.Programmatic);
    }

    private bool IsAppMode => KindSelector.SelectedItem == AppKind;

    private void PlaceOnPrimaryDisplay()
    {
        var area = DisplayArea.Primary.WorkArea;
        var scale = NativeWindow.GetDpiScale(WinRT.Interop.WindowNative.GetWindowHandle(this));
        var width = (int)(WidthDips * scale);
        var height = (int)(HeightDips * scale);
        AppWindow.MoveAndResize(new RectInt32(area.X + ((area.Width - width) / 2), area.Y + ((area.Height - height) / 2), width, height));
    }

    private async void OnKindChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        WebPanel.Visibility = IsAppMode ? Visibility.Collapsed : Visibility.Visible;
        AppPanel.Visibility = IsAppMode ? Visibility.Visible : Visibility.Collapsed;
        ErrorBar.IsOpen = false;
        UpdateAddButton();
        (IsAppMode ? SearchBox : AddressBox).Focus(FocusState.Programmatic);
        if (IsAppMode && _apps is null)
        {
            await LoadAppsAsync();
        }
    }

    private async Task LoadAppsAsync()
    {
        AppsLoading.IsActive = true;
        try
        {
            var apps = await Task.Run(InstalledApps.Enumerate);
            _apps = apps
                .DistinctBy(a => a.ParsingName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(a => new AppChoice(a))
                .ToList();
            ApplyFilter();
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            _logger.LogWarning(ex, "Não foi possível listar os apps instalados");
            ShowError("AddLinkAppsFailed");
        }
        finally
        {
            AppsLoading.IsActive = false;
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (_apps is null)
        {
            return;
        }

        var query = SearchBox.Text.Trim();
        AppList.ItemsSource = query.Length == 0
            ? _apps
            : _apps.Where(a => a.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private void OnAppContainerChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is AppChoice choice)
        {
            _ = choice.EnsureIconAsync();
        }
    }

    private void OnAppSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateAddButton();

    private void OnAppDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => TryAdd();

    private void OnAddressKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            TryAdd();
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => TryAdd();

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void UpdateAddButton() => AddButton.IsEnabled = !IsAppMode || AppList.SelectedItem is AppChoice;

    private void TryAdd()
    {
        ErrorBar.IsOpen = false;
        QuickLink link;
        if (IsAppMode)
        {
            if (AppList.SelectedItem is not AppChoice choice)
            {
                return;
            }

            link = QuickLink.ForInstalledApp(choice.Name, choice.App.ParsingName);
        }
        else
        {
            if (!WebAddress.TryNormalize(AddressBox.Text, out var uri))
            {
                ShowError("AddLinkInvalidAddress");
                return;
            }

            var name = NameBox.Text.Trim();
            link = QuickLink.Create(QuickLinkKind.Web, name.Length > 0 ? name : WebAddress.DefaultName(uri), uri.AbsoluteUri);
        }

        if (_catalog.Links.Any(l => l.SameTarget(link)))
        {
            ShowError("AddLinkDuplicate");
            return;
        }

        if (_catalog.Links.Count >= QuickLinkList.MaxLinks)
        {
            ShowError("AddLinkFull");
            return;
        }

        if (!_catalog.Add(link))
        {
            ShowError("AddLinkSaveFailed");
            return;
        }

        Close();
    }

    private void ShowError(string key)
    {
        ErrorBar.Message = ModuleStrings.Get(key);
        ErrorBar.IsOpen = true;
    }
}
