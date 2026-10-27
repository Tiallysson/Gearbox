using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Gearbox.Modules.Notch.Native;

/// <summary>
/// Acrílico (desfoque do que está atrás) que fica sempre com a aparência de janela ativa.
/// </summary>
/// <remarks>
/// O <c>DesktopAcrylicBackdrop</c> do XAML acompanha a ativação da janela e troca o desfoque por uma cor sólida
/// quando ela está inativa. A ilha nunca é ativada (<c>WS_EX_NOACTIVATE</c>), então ele ficaria sempre sólido:
/// aqui o controlador recebe uma configuração fixa, ativa e escura.
/// <para>
/// O acrílico só desfoca: tinta e luminosidade zeradas, e a reserva (efeitos de transparência desligados, economia
/// de energia) é transparente. Toda a escuridão vem do tom da ilha (<c>IslandBrush</c>), o mesmo das "orelhas" de
/// cima, que não podem ter acrílico (o <c>SystemBackdropElement</c> não recorta cantos côncavos). Assim corpo e
/// orelhas têm a mesma cor em qualquer configuração; só o desfoque fica restrito ao corpo.
/// </para>
/// </remarks>
internal sealed partial class AcrylicBackdrop : SystemBackdrop
{
    private static readonly Windows.UI.Color Transparent = Windows.UI.Color.FromArgb(0, 0, 0, 0);

    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configuration;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (!DesktopAcrylicController.IsSupported())
        {
            return;
        }

        _configuration = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Dark,
        };
        _controller = new DesktopAcrylicController
        {
            Kind = DesktopAcrylicKind.Thin,
            TintColor = Transparent,
            TintOpacity = 0,
            LuminosityOpacity = 0,
            FallbackColor = Transparent,
        };
        _controller.AddSystemBackdropTarget(connectedTarget);
        _controller.SetSystemBackdropConfiguration(_configuration);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        if (_controller is not null)
        {
            _controller.RemoveSystemBackdropTarget(disconnectedTarget);
            _controller.Dispose();
            _controller = null;
        }

        _configuration = null;
    }
}
