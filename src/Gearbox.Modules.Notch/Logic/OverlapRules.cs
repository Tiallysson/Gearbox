using Gearbox.Interop;

namespace Gearbox.Modules.Notch.Logic;

/// <summary>Como a ilha deve aparecer, dado o que está na frente.</summary>
public enum NotchVisibility
{
    Normal,

    /// <summary>A janela em foco passa por baixo da ilha: recolhida vira só uma alça.</summary>
    Compact,

    /// <summary>Tela cheia (jogo, vídeo, apresentação): a ilha some.</summary>
    Hidden,
}

/// <summary>A janela em primeiro plano como as regras a enxergam.</summary>
public readonly record struct ForegroundWindow(PixelRect Bounds, string ClassName, bool IsMinimized, bool IsCloaked, bool IsOwnProcess);

/// <summary>Decide se a ilha some, encolhe ou aparece normalmente.</summary>
public static class OverlapRules
{
    private static readonly HashSet<string> s_shellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
    };

    /// <param name="state">Estado do shell (<c>SHQueryUserNotificationState</c>).</param>
    /// <param name="foreground">Janela em primeiro plano, ou <c>null</c> se não há nenhuma.</param>
    /// <param name="monitor">Monitor da ilha.</param>
    /// <param name="island">Retângulo da ilha recolhida (na maior forma, para não oscilar).</param>
    public static NotchVisibility Evaluate(
        UserNotificationState state,
        ForegroundWindow? foreground,
        PixelRect monitor,
        PixelRect island,
        bool hideOnFullscreen,
        bool compactWhenCovered)
    {
        if (hideOnFullscreen && state is UserNotificationState.Busy
            or UserNotificationState.RunningDirect3DFullScreen
            or UserNotificationState.PresentationMode)
        {
            return NotchVisibility.Hidden;
        }

        if (foreground is not { } window || !IsRelevant(window))
        {
            return NotchVisibility.Normal;
        }

        if (hideOnFullscreen && IsFullscreen(window.Bounds, monitor))
        {
            return NotchVisibility.Hidden;
        }

        return compactWhenCovered && Intersects(window.Bounds, island) ? NotchVisibility.Compact : NotchVisibility.Normal;
    }

    /// <summary>Janela "de verdade" de outro app (não a área de trabalho, a barra de tarefas ou o próprio Gearbox).</summary>
    public static bool IsRelevant(ForegroundWindow window) =>
        !window.IsMinimized &&
        !window.IsCloaked &&
        !window.IsOwnProcess &&
        !window.Bounds.IsEmpty &&
        !s_shellClasses.Contains(window.ClassName);

    /// <summary>Cobre o monitor inteiro, incluindo a barra de tarefas.</summary>
    public static bool IsFullscreen(PixelRect window, PixelRect monitor) =>
        window.Left <= monitor.Left &&
        window.Top <= monitor.Top &&
        window.Right >= monitor.Right &&
        window.Bottom >= monitor.Bottom;

    public static bool Intersects(PixelRect a, PixelRect b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
}
