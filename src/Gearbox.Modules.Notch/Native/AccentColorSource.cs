using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace Gearbox.Modules.Notch.Native;

internal sealed class AccentColorSource : IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly UISettings _settings = new();
    private bool _disposed;

    public AccentColorSource(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        _settings.ColorValuesChanged += OnColorValuesChanged;
    }

    public event EventHandler? Changed;

    public TintColor Current
    {
        get
        {
            var color = _settings.GetColorValue(UIColorType.AccentDark2);
            return new TintColor(color.R, color.G, color.B);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.ColorValuesChanged -= OnColorValuesChanged;
    }

    private void OnColorValuesChanged(UISettings sender, object args) =>
        _dispatcher.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        });
}
