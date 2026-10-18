using Gearbox.Interop;

namespace Gearbox.Modules.Wallpaper.Logic;

/// <summary>Uma janela de topo como as regras de cobertura a enxergam.</summary>
public readonly record struct DesktopWindow(
    PixelRect Bounds,
    string ClassName,
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked,
    bool IsToolWindow,
    bool IsClickThrough,
    bool IsOwnProcess);

/// <summary>Um monitor: área inteira e área de trabalho (sem a barra de tarefas).</summary>
public readonly record struct MonitorArea(string DevicePath, PixelRect Bounds, PixelRect WorkArea);

/// <summary>Decide se o wallpaper de um monitor está escondido por uma janela (tela cheia ou maximizada).</summary>
public static class CoverageRules
{
    /// <summary>
    /// Folga, por lado, em relação à área de trabalho. Cobre as bordas invisíveis de janelas maximizadas
    /// (cerca de 8 px para fora) e as pseudo-maximizadas pelo RoundedMaximize (margem de até 64 px para dentro).
    /// </summary>
    public const int WorkAreaTolerance = 72;

    private static readonly HashSet<string> s_shellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
    };

    /// <summary>Janelas que podem esconder o wallpaper: visíveis, de outro app e "de verdade" (não overlays).</summary>
    public static bool IsCandidate(DesktopWindow window) =>
        window.IsVisible &&
        !window.IsMinimized &&
        !window.IsCloaked &&
        !window.IsToolWindow &&
        !window.IsClickThrough &&
        !window.IsOwnProcess &&
        !window.Bounds.IsEmpty &&
        !s_shellClasses.Contains(window.ClassName);

    /// <summary>A janela ocupa o monitor inteiro (tela cheia) ou praticamente a área de trabalho (maximizada).</summary>
    public static bool Covers(PixelRect window, MonitorArea monitor)
    {
        var fullscreen =
            window.Left <= monitor.Bounds.Left &&
            window.Top <= monitor.Bounds.Top &&
            window.Right >= monitor.Bounds.Right &&
            window.Bottom >= monitor.Bounds.Bottom;

        if (fullscreen)
        {
            return true;
        }

        var work = monitor.WorkArea;
        return Math.Abs(window.Left - work.Left) <= WorkAreaTolerance &&
            Math.Abs(window.Top - work.Top) <= WorkAreaTolerance &&
            Math.Abs(window.Right - work.Right) <= WorkAreaTolerance &&
            Math.Abs(window.Bottom - work.Bottom) <= WorkAreaTolerance;
    }

    /// <summary>Device paths dos monitores cobertos por alguma janela candidata.</summary>
    public static IReadOnlySet<string> CoveredMonitors(IEnumerable<DesktopWindow> windows, IReadOnlyList<MonitorArea> monitors) =>
        new HashSet<string>(CoveringWindows(windows, monitors).Keys, StringComparer.OrdinalIgnoreCase);

    /// <summary>Para cada monitor coberto, a primeira janela (na ordem recebida) que o cobre.</summary>
    public static IReadOnlyDictionary<string, DesktopWindow> CoveringWindows(IEnumerable<DesktopWindow> windows, IReadOnlyList<MonitorArea> monitors)
    {
        var covering = new Dictionary<string, DesktopWindow>(StringComparer.OrdinalIgnoreCase);
        foreach (var window in windows)
        {
            if (!IsCandidate(window))
            {
                continue;
            }

            foreach (var monitor in monitors)
            {
                if (!covering.ContainsKey(monitor.DevicePath) && Covers(window.Bounds, monitor))
                {
                    covering[monitor.DevicePath] = window;
                }
            }

            if (covering.Count == monitors.Count)
            {
                break;
            }
        }

        return covering;
    }
}
