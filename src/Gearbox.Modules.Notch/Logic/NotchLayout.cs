using Gearbox.Interop;

namespace Gearbox.Modules.Notch.Logic;

/// <summary>Tamanho em pixels independentes de DPI (1 DIP = 1 px a 96 DPI).</summary>
public readonly record struct DipSize(double Width, double Height)
{
    public static DipSize Max(DipSize a, DipSize b) => new(Math.Max(a.Width, b.Width), Math.Max(a.Height, b.Height));
}

/// <summary>
/// Forma da ilha: tamanho do corpo, raio dos cantos de baixo e raio das "orelhas" de cima, os cantos côncavos que
/// escorrem para a borda da tela (como o notch do MacBook). As orelhas ficam fora do corpo, uma de cada lado.
/// </summary>
public readonly record struct NotchShape(DipSize Size, double CornerRadius, double EarRadius);

/// <summary>Tamanhos da ilha em cada modo e onde ela fica na tela.</summary>
public static class NotchLayout
{
    // Recolhida, sem orelhas: fica no topo o tempo todo e não pode ocupar nem um pixel a mais (abas, botões).
    public static readonly NotchShape Idle = new(new DipSize(180, 30), 12, 0);
    public static readonly NotchShape Playing = new(new DipSize(236, 30), 12, 0);
    public static readonly NotchShape Handle = new(new DipSize(120, 6), 3, 0);
    public static readonly NotchShape PeekShape = new(new DipSize(320, 40), 18, 10);
    public static readonly NotchShape ExpandedShape = new(new DipSize(420, 156), 28, 14);

    /// <summary>Aberta com as abas "Mídia" / "Controles": a linha das abas e os três controles precisam de altura.</summary>
    public static readonly NotchShape ExpandedTabbedShape = new(new DipSize(420, 204), 28, 14);

    /// <summary>Tamanho da janela para um corpo de <paramref name="size"/> com orelhas de <paramref name="earRadius"/>.</summary>
    public static DipSize WindowSize(DipSize size, double earRadius) =>
        new(size.Width + (2 * Math.Max(0, earRadius)), size.Height);

    public static DipSize WindowSize(NotchShape shape) => WindowSize(shape.Size, shape.EarRadius);

    /// <summary>
    /// Forma para o estado atual. <paramref name="compact"/> = recolhida sobre uma janela: vira só uma alça,
    /// para não cobrir abas e botões (os avisos e a ilha aberta continuam no tamanho normal).
    /// <paramref name="withTabs"/> = a ilha aberta mostra as abas (algum controle rápido ligado).
    /// </summary>
    public static NotchShape ShapeFor(NotchMode mode, bool isPlaying, bool compact, bool withTabs = false) => mode switch
    {
        NotchMode.Expanded => withTabs ? ExpandedTabbedShape : ExpandedShape,
        NotchMode.Peek => PeekShape,
        _ when compact => Handle,
        _ => isPlaying ? Playing : Idle,
    };

    /// <summary>Retângulo, em pixels físicos, de uma ilha centralizada no topo do monitor.</summary>
    public static PixelRect Place(PixelRect monitor, double dpiScale, DipSize size)
    {
        var scale = dpiScale > 0 ? dpiScale : 1.0;
        var width = Math.Max(1, (int)Math.Ceiling(size.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(size.Height * scale));
        var left = monitor.Left + ((monitor.Width - width) / 2);
        return PixelRect.FromSize(left, monitor.Top, width, height);
    }
}
