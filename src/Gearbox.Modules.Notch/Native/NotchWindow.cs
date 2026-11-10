using Gearbox.Interop.Geometry;
using Gearbox.Interop.Windowing;
using Gearbox.Modules.Notch.Logic;
using Gearbox.Modules.Notch.UI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Gearbox.Modules.Notch.Native;

/// <summary>
/// Janela da ilha: WinUI sem moldura, sempre no topo, centralizada no topo do monitor, que não rouba o foco e
/// não aparece no Alt+Tab. O fundo é transparente: só a forma preta da <see cref="NotchView"/> aparece.
/// </summary>
internal sealed class NotchWindow : IDisposable
{
    private readonly ILogger _logger;
    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private readonly nint _hwnd;
    private readonly TransparentWindow? _transparency;
    private DisplayMonitor _monitor;
    private static readonly DipSize s_windowSize = NotchLayout.MaxWindowSize;

    private NotchShape _shape = NotchLayout.Idle;
    private PixelRect _windowBounds;
    private PixelRect _bounds;
    private NotchRegionShape _region;
    private bool _visible;
    private bool _disposed;

    public NotchWindow(DisplayMonitor monitor, ILogger logger)
    {
        _logger = logger;
        _monitor = monitor;
        View = new NotchView();

        _window = new Window
        {
            Title = "Gearbox Notch",
            Content = View,
            SystemBackdrop = new TransparentBackdrop(),
        };
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);

        _appWindow = _window.AppWindow;
        _appWindow.IsShownInSwitchers = false;
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.PreferredMinimumWidth = 1;
            presenter.PreferredMinimumHeight = 1;
        }

        NativeWindow.MakeOverlay(_hwnd);
        NativeWindow.SetCornerPreference(_hwnd, CornerPreference.DoNotRound);
        _transparency = TransparentWindow.Apply(_hwnd);
        if (_transparency is null)
        {
            _logger.LogWarning("O DWM recusou a transparência; a ilha fica com fundo opaco");
        }

        View.SetShape(_shape, animate: false);
        View.ShapeFrame += OnShapeFrame;
        PlaceWindow();
        SetRegion(_shape.Size, _shape.EarRadius);
    }

    public NotchView View { get; }

    public bool IsVisible => _visible;

    public bool IsTransparent => _transparency is not null;

    /// <summary>Retângulo atual da janela, em pixels físicos.</summary>
    public PixelRect Bounds => _bounds;

    public void SetMonitor(DisplayMonitor monitor)
    {
        _monitor = monitor;
        PlaceWindow();
        _region = default;
        SetRegion(View.CurrentSize, View.CurrentEarRadius);
    }

    public void SetShape(NotchShape shape, bool animate)
    {
        if (_disposed || (shape == _shape && !View.IsAnimatingShape))
        {
            return;
        }

        _shape = shape;
        if (!_visible || !animate)
        {
            View.SetShape(shape, animate: false);
            SetRegion(shape.Size, shape.EarRadius);
            return;
        }

        View.SetShape(shape, animate: true, () =>
        {
            if (!_disposed && _shape == shape)
            {
                SetRegion(shape.Size, shape.EarRadius);
            }
        });
    }

    public void SetVisible(bool visible)
    {
        if (_disposed || visible == _visible)
        {
            return;
        }

        _visible = visible;
        if (visible)
        {
            _appWindow.Show(activateWindow: false);
        }
        else
        {
            _appWindow.Hide();
        }
    }

    /// <summary>O cursor está sobre a janela (rede de segurança para um <c>PointerExited</c> perdido).</summary>
    public bool IsCursorInside() =>
        NativeWindow.TryGetCursorPosition(out var x, out var y) &&
        x >= _bounds.Left && x < _bounds.Right && y >= _bounds.Top && y < _bounds.Bottom;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        View.ShapeFrame -= OnShapeFrame;
        View.StopAnimations();
        View.ReleaseBackdrops();
        _window.Close();
    }

    private void PlaceWindow()
    {
        var bounds = NotchLayout.Place(_monitor.Bounds, _monitor.DpiScale, s_windowSize);
        if (bounds == _windowBounds)
        {
            return;
        }

        _windowBounds = bounds;
        _appWindow.MoveAndResize(new RectInt32(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
    }

    private void OnShapeFrame(object? sender, NotchShape frame)
    {
        if (!_disposed)
        {
            SetRegion(frame.Size, frame.EarRadius);
        }
    }

    private void SetRegion(DipSize body, double earRadius)
    {
        var region = NotchRegion.Compute(_windowBounds, _monitor.Bounds, _monitor.DpiScale, body, earRadius);
        if (region == _region)
        {
            return;
        }

        _region = region;
        _bounds = region.Bounds;
        if (!NativeWindow.TrySetRegion(_hwnd, region.Relative, [region.LeftCutout, region.RightCutout], [region.LeftGap, region.RightGap]))
        {
            _logger.LogWarning("Não foi possível recortar a janela da ilha em {Bounds}", region.Bounds);
        }
    }
}
