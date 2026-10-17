namespace Darwin.Interop;

/// <summary>Subconjunto de <c>SHOW_WINDOW_CMD</c> usado com <c>WINDOWPLACEMENT</c>.</summary>
public enum ShowWindowCommand : uint
{
    Normal = 1,
    ShowMinimized = 2,
    ShowMaximized = 3,
    ShowNoActivate = 4,
    ShowMinNoActive = 7,
}

/// <summary>
/// Cópia gerenciada de um <c>WINDOWPLACEMENT</c>. <see cref="NormalPosition"/> está em coordenadas de
/// área de trabalho (workspace), não de tela, para janelas top-level sem <c>WS_EX_TOOLWINDOW</c>.
/// </summary>
public readonly record struct WindowPlacement(
    ShowWindowCommand ShowCommand,
    PixelRect NormalPosition,
    uint Flags,
    int MinPositionX,
    int MinPositionY,
    int MaxPositionX,
    int MaxPositionY);
