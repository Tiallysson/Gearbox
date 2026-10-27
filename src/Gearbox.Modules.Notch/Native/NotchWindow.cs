using Gearbox.Interop;
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
/// <remarks>
/// A janela tem o tamanho da forma, para não bloquear cliques ao redor. Durante uma animação ela fica com o
/// maior dos dois tamanhos (o de antes e o de depois) e encolhe quando a animação termina.
/// </remarks>
internal sealed class NotchWindow : IDisposable
{
    private readonly ILogger _logger;
    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private readonly nint _hwnd;
    private readonly TransparentWindow? _transparency; // Campo: precisa viver enquanto a janela existir.
    private DisplayMonitor _monitor;
    private NotchShape _shape = NotchLayout.Idle;
    private PixelRect _bounds;
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
        Resize(NotchLayout.WindowSize(_shape));
    }

    public NotchView View { get; }

    /// <summary>Retângulo atual da janela, em pixels físicos.</summary>
    public PixelRect Bounds => _bounds;

    public void SetMonitor(DisplayMonitor monitor)
    {
        _monitor = monitor;
        Resize(DipSize.Max(NotchLayout.WindowSize(_shape), View.CurrentWindowSize));
    }

    public void SetShape(NotchShape shape, bool animate)
    {
        if (_disposed)
        {
            return;
        }

        _shape = shape;
        if (!_visible || !animate)
        {
            View.SetShape(shape, animate: false);
            Resize(NotchLayout.WindowSize(shape));
            return;
        }

        // Cresce a janela antes; encolhe só depois que a forma terminar de encolher.
        Resize(DipSize.Max(NotchLayout.WindowSize(shape), View.CurrentWindowSize));
        View.SetShape(shape, animate: true, () =>
        {
            if (!_disposed && _shape == shape)
            {
                Resize(NotchLayout.WindowSize(shape));
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
        _window.Close();
    }

    private void Resize(DipSize size)
    {
        var bounds = NotchLayout.Place(_monitor.Bounds, _monitor.DpiScale, size);
        if (bounds == _bounds)
        {
            return;
        }

        _bounds = bounds;
        _appWindow.MoveAndResize(new RectInt32(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
    }
}
