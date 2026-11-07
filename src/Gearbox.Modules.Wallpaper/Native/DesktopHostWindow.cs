using Gearbox.Interop.Desktop;
using Gearbox.Interop.Geometry;
using Gearbox.Interop.Windowing;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Gearbox.Modules.Wallpaper.Native;

internal sealed class DesktopHostWindow : IDisposable
{
    private static readonly TimeSpan AttachDelay = TimeSpan.FromMilliseconds(300);

    private readonly ILogger _logger;
    private readonly Window _window;
    private readonly nint _hwnd;
    private readonly DispatcherQueueTimer _attachTimer;
    private PixelRect _bounds;
    private bool _disposed;

    public DesktopHostWindow(UIElement content, PixelRect bounds, ILogger logger)
    {
        _logger = logger;
        _bounds = bounds;
        var dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("O wallpaper precisa ser criado na thread de UI.");

        _window = new Window { Content = content };
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);

        var appWindow = _window.AppWindow;
        appWindow.IsShownInSwitchers = false;
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        appWindow.MoveAndResize(new RectInt32(-32000, -32000, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height)));
        appWindow.Show(activateWindow: false);

        _attachTimer = dispatcher.CreateTimer();
        _attachTimer.Interval = AttachDelay;
        _attachTimer.IsRepeating = false;
        _attachTimer.Tick += (_, _) => Attach();
        _attachTimer.Start();
    }

    public void SetBounds(PixelRect bounds)
    {
        _bounds = bounds;
        Attach();
    }

    public void Attach()
    {
        if (_disposed)
        {
            return;
        }

        var host = DesktopLayer.FindHost();
        if (host == 0)
        {
            _logger.LogWarning("Camada da área de trabalho não encontrada; o wallpaper fica oculto até o Explorer voltar");
            return;
        }

        if (!DesktopLayer.Attach(_hwnd, host, _bounds))
        {
            _logger.LogWarning("Não foi possível prender o wallpaper na área de trabalho (erro {Error})", NativeWindow.LastError);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _attachTimer.Stop();
        _window.Close();
    }
}
