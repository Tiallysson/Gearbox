using Gearbox.Interop;

namespace Gearbox.Modules.RoundedMaximize.Logic;

/// <summary>Cálculo do retângulo "pseudo-maximizado".</summary>
public static class PseudoMaximizeLayout
{
    public const int MinMargin = 0;
    public const int MaxMargin = 64;

    public static int ClampMargin(int margin) => Math.Clamp(margin, MinMargin, MaxMargin);

    /// <summary>
    /// Retângulo a passar para <c>SetWindowPos</c> para que a moldura <em>visível</em> fique a
    /// <paramref name="margin"/> px de cada borda da área de trabalho.
    /// </summary>
    /// <param name="workArea">Área de trabalho do monitor (<c>rcWork</c>), em coordenadas de tela.</param>
    /// <param name="margin">Margem desejada; é limitada a 0–64.</param>
    /// <param name="insets">Bordas invisíveis da janela (diferença entre o retângulo da janela e a moldura visível).</param>
    public static PixelRect Compute(PixelRect workArea, int margin, FrameInsets insets)
    {
        margin = ClampMargin(margin);

        // Se a margem não couber (monitor minúsculo), usa a área inteira.
        if (workArea.Width - (2 * margin) < 1 || workArea.Height - (2 * margin) < 1)
        {
            margin = 0;
        }

        return new PixelRect(
            workArea.Left + margin - insets.Left,
            workArea.Top + margin - insets.Top,
            workArea.Right - margin + insets.Right,
            workArea.Bottom - margin + insets.Bottom);
    }
}
