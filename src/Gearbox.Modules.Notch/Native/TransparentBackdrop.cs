using Gearbox.Interop;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Gearbox.Modules.Notch.Native;

/// <summary>
/// Backdrop totalmente transparente: o XAML deixa de pintar o fundo preto por baixo do conteúdo. Funciona junto
/// com <see cref="TransparentWindow"/>, que tira o fundo da janela Win32.
/// </summary>
internal sealed partial class TransparentBackdrop : SystemBackdrop
{
    private Windows.UI.Composition.Compositor? _compositor;
    private Windows.UI.Composition.CompositionColorBrush? _brush;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        // O pincel precisa vir do compositor do sistema, que exige um Windows.System.DispatcherQueue na thread.
        TransparentWindow.EnsureSystemDispatcherQueue();
        _compositor ??= new Windows.UI.Composition.Compositor();
        _brush ??= _compositor.CreateColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        connectedTarget.SystemBackdrop = _brush;
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        disconnectedTarget.SystemBackdrop = null;

        _brush?.Dispose();
        _brush = null;
        _compositor?.Dispose();
        _compositor = null;
    }
}
