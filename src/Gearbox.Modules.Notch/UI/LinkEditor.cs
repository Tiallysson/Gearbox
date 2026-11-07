using Gearbox.Interop.Windowing;
using Gearbox.Modules.Notch.Native;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Gearbox.Modules.Notch.UI;

public sealed class LinkEditor(LinkCatalog catalog, ILogger<LinkEditor> logger)
{
    private AddLinkWindow? _window;

    public void Open()
    {
        if (_window is null)
        {
            _window = new AddLinkWindow(catalog, logger);
            _window.Closed += OnClosed;
        }

        _window.Activate();
        NativeWindow.BringToFront(WinRT.Interop.WindowNative.GetWindowHandle(_window));
    }

    public void Close() => _window?.Close();

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (_window is not null)
        {
            _window.Closed -= OnClosed;
            _window = null;
        }
    }
}
