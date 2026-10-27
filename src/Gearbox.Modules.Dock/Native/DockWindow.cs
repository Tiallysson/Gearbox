using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Gearbox.Interop;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

namespace Gearbox.Modules.Dock.Native;

/// <summary>Dock flutuante no monitor principal, acima da barra de tarefas, sem alterar o shell.</summary>
internal sealed class DockWindow : IDisposable
{
    private readonly Window _window;
    private readonly TransparentWindow? _transparency;
    private readonly StackPanel _items = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly TextBlock _clock = new() { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _date = new() { FontSize = 11 };
    private readonly TextBlock _focus = new() { FontSize = 19, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _focusLabel = new() { FontSize = 10 };
    private readonly TextBlock _status = new() { FontSize = 11, MaxWidth = 180, TextWrapping = TextWrapping.Wrap };
    private readonly Border _surface;
    private readonly ScrollViewer _scroll;
    private DockSettings _settings = new();
    private DateTimeOffset? _focusEnd;
    private TimeSpan _remaining = TimeSpan.FromMinutes(25);
    private PixelRect _bounds;
    private bool _disposed;

    public DockWindow()
    {
        _surface = new Border
        {
            CornerRadius = new CornerRadius(22),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = _items,
        };
        _scroll = new ScrollViewer
        {
            Content = _surface,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
        };
        _window = new Window { Title = "Gearbox Dock", Content = _scroll, SystemBackdrop = new TransparentBackdrop() };
        try
        {
            _window.AppWindow.IsShownInSwitchers = false;
            if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.PreferredMinimumWidth = 1;
                presenter.PreferredMinimumHeight = 1;
            }
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            NativeWindow.MakeOverlay(hwnd);
            NativeWindow.SetCornerPreference(hwnd, CornerPreference.DoNotRound);
            _transparency = TransparentWindow.Apply(hwnd);
        }
        catch { _window.Close(); throw; }
    }

    public void Apply(DockSettings settings)
    {
        _settings = settings;
        _surface.RequestedTheme = settings.DarkTheme ? ElementTheme.Dark : ElementTheme.Light;
        _surface.Background = Brush(settings.DarkTheme ? 0xED : 0xF2, settings.DarkTheme ? 24 : 246, settings.DarkTheme ? 29 : 242, settings.DarkTheme ? 40 : 234);
        _surface.BorderBrush = Brush(90, 155, 165, 185);
        // Os textos são reutilizados; solta o pai anterior antes de montar os novos cartões.
        foreach (var text in new[] { _date, _clock, _focusLabel, _focus })
        {
            if (text.Parent is Panel parent) parent.Children.Remove(text);
        }
        _items.Children.Clear();
        AddLauncher("Explorer", "\uE8B7", 242, 183, 52, () => OpenProcess("explorer.exe"));
        AddLauncher("Browser", "\uE774", 45, 174, 224, () => OpenUri("https://www.bing.com"));
        AddLauncher("Terminal", "\uE756", 55, 63, 79, () => OpenProcess("cmd.exe"));
        AddLauncher("Notepad", "\uE70B", 42, 164, 201, () => OpenProcess("notepad.exe"));
        AddLauncher("Calculator", "\uE8EF", 89, 108, 141, () => OpenUri("calculator:"));
        AddLauncher("Settings", "\uE713", 111, 122, 142, () => OpenUri("ms-settings:"));
        _items.Children.Add(new Border { Width = 1, Margin = new Thickness(4, 5, 4, 5), Background = Brush(70, 140, 150, 170) });
        var calendar = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Center };
        calendar.Children.Add(_date);
        calendar.Children.Add(_clock);
        if (settings.ShowClock) _items.Children.Add(Card(calendar, "Calendar", () => OpenUri("ms-clock:")));
        var focusPanel = new StackPanel { Spacing = 1 };
        focusPanel.Children.Add(_focusLabel);
        focusPanel.Children.Add(_focus);
        if (settings.ShowFocus)
        {
            var focusButton = Card(focusPanel, "FocusTip", ToggleFocus);
            var menu = new MenuFlyout();
            var reset = new MenuFlyoutItem { Text = ModuleStrings.Get("ResetFocus") };
            reset.Click += (_, _) => { _focusEnd = null; _remaining = TimeSpan.FromMinutes(25); UpdateText(); };
            menu.Items.Add(reset);
            focusButton.ContextFlyout = menu;
            _items.Children.Add(focusButton);
        }
        AddLauncher("Volume", "\uE767", 117, 104, 177, () => OpenUri("ms-settings:sound"));
        AddLauncher("RecycleBin", "\uE74D", 92, 147, 144, () =>
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = false };
            start.ArgumentList.Add("shell:RecycleBinFolder");
            using var process = Process.Start(start);
        });
        if (!string.IsNullOrEmpty(_status.Text)) _items.Children.Add(_status);
        UpdateText();
        Place();
        _window.AppWindow.Show(activateWindow: false);
    }

    public void Tick()
    {
        if (_disposed) return;
        UpdateText();
        // Reavalia DPI, monitor e área útil, inclusive quando a barra de tarefas muda de posição.
        Place();
    }

    private void UpdateText()
    {
        var now = DateTime.Now;
        _clock.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        _date.Text = now.ToString("ddd, dd MMM", CultureInfo.CurrentCulture);
        var remaining = _focusEnd is { } end ? end - DateTimeOffset.UtcNow : _remaining;
        if (remaining <= TimeSpan.Zero)
        {
            _focusEnd = null;
            _remaining = TimeSpan.Zero;
            remaining = TimeSpan.Zero;
        }
        _focusLabel.Text = ModuleStrings.Get(_remaining == TimeSpan.Zero && _focusEnd is null ? "FocusDone" : _focusEnd is null ? "FocusPaused" : "FocusRunning");
        _focus.Text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
    }

    private void ToggleFocus()
    {
        if (_focusEnd is { } end)
        {
            _remaining = end - DateTimeOffset.UtcNow;
            if (_remaining < TimeSpan.Zero) _remaining = TimeSpan.Zero;
            _focusEnd = null;
        }
        else
        {
            if (_remaining <= TimeSpan.Zero) _remaining = TimeSpan.FromMinutes(25);
            _focusEnd = DateTimeOffset.UtcNow + _remaining;
        }
        UpdateText();
    }

    private void AddLauncher(string label, string glyph, byte r, byte g, byte b, Action launch)
    {
        var size = Math.Clamp(_settings.IconSize, 32, 56);
        var button = new Button
        {
            Width = size, Height = size, Padding = new Thickness(0), CornerRadius = new CornerRadius(12),
            Background = Brush(255, r, g, b), BorderThickness = new Thickness(0),
            Content = new FontIcon { Glyph = glyph, FontSize = size * 0.53, Foreground = Brush(255, 255, 255, 255) },
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(button, ModuleStrings.Get(label));
        AutomationProperties.SetName(button, ModuleStrings.Get(label));
        button.Click += (_, _) => Run(launch);
        button.PointerEntered += (_, _) => Animate(button, 1.10f);
        button.PointerExited += (_, _) => Animate(button, 1);
        _items.Children.Add(button);
    }

    private static void Animate(Button button, float scale)
    {
        var visual = ElementCompositionPreview.GetElementVisual(button);
        visual.CenterPoint = new Vector3((float)button.ActualWidth / 2, (float)button.ActualHeight / 2, 0);
        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(1, new Vector3(scale, scale, 1));
        animation.Duration = TimeSpan.FromMilliseconds(140);
        visual.StartAnimation("Scale", animation);
    }

    private Button Card(UIElement content, string label, Action action)
    {
        var button = new Button { Content = content, MinWidth = 110, Height = Math.Clamp(_settings.IconSize, 32, 56), Padding = new Thickness(12, 0, 12, 0), CornerRadius = new CornerRadius(12) };
        ToolTipService.SetToolTip(button, ModuleStrings.Get(label));
        AutomationProperties.SetName(button, ModuleStrings.Get(label));
        button.Click += (_, _) => Run(action);
        return button;
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception)
        {
            _status.Text = ModuleStrings.Get("LaunchFailed");
            ToolTipService.SetToolTip(_surface, _status.Text);
            if (!_items.Children.Contains(_status)) _items.Children.Add(_status);
            Place();
        }
    }

    private static void OpenProcess(string file)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), file);
        if (file == "cmd.exe") path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), file);
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async void OpenUri(string uri)
    {
        try
        {
            if (!await Windows.System.Launcher.LaunchUriAsync(new Uri(uri))) Run(() => throw new InvalidOperationException());
        }
        catch (Exception) { Run(() => throw new InvalidOperationException()); }
    }

    private void Place()
    {
        if (!NativeWindow.TryGetPrimaryMonitor(out var monitor)) return;
        var area = monitor.WorkArea;
        var scale = monitor.DpiScale;
        _surface.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = Math.Min(area.Width, (int)Math.Ceiling(_surface.DesiredSize.Width * scale));
        var height = Math.Min(area.Height, (int)Math.Ceiling(_surface.DesiredSize.Height * scale));
        var margin = (int)(Math.Clamp(_settings.BottomMargin, 0, 64) * scale);
        var bounds = PixelRect.FromSize(area.Left + (area.Width - width) / 2, Math.Max(area.Top, area.Bottom - height - margin), Math.Max(1, width), Math.Max(1, height));
        if (bounds == _bounds) return;
        _bounds = bounds;
        _window.AppWindow.MoveAndResize(new RectInt32(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
    }

    private static SolidColorBrush Brush(byte a, byte r, byte g, byte b) => new(Color.FromArgb(a, r, g, b));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _focusEnd = null;
        // Mantém o controlador nativo vivo até o fechamento da janela.
        _window.Close();
        GC.KeepAlive(_transparency);
    }
}
