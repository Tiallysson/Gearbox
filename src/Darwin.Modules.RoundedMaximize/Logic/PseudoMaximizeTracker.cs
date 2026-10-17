using Darwin.Interop;

namespace Darwin.Modules.RoundedMaximize.Logic;

/// <summary>Uma janela que o módulo "pseudo-maximizou".</summary>
/// <param name="Hwnd">Handle da janela.</param>
/// <param name="OriginalPlacement">Placement lido quando a janela estava maximizada (o <c>NormalPosition</c> é o tamanho original).</param>
/// <param name="PseudoNormalPosition"><c>rcNormalPosition</c> depois de aplicar o retângulo pseudo-maximizado (coordenadas de workspace).</param>
/// <param name="PseudoWindowRect">Retângulo de tela depois de aplicar o pseudo-maximizar.</param>
public sealed record TrackedWindow(
    nint Hwnd,
    WindowPlacement OriginalPlacement,
    PixelRect PseudoNormalPosition,
    PixelRect PseudoWindowRect)
{
    /// <summary>
    /// Retângulo original em coordenadas de tela. O deslocamento workspace → tela é o mesmo que existe
    /// entre os dois retângulos pseudo-maximizados, capturados juntos com a janela em estado normal.
    /// </summary>
    public PixelRect OriginalWindowRect
    {
        get
        {
            var dx = PseudoWindowRect.Left - PseudoNormalPosition.Left;
            var dy = PseudoWindowRect.Top - PseudoNormalPosition.Top;
            var normal = OriginalPlacement.NormalPosition;
            return new PixelRect(normal.Left + dx, normal.Top + dy, normal.Right + dx, normal.Bottom + dy);
        }
    }
}

public enum MaximizeAction
{
    /// <summary>Maximizar "novo": guardar o tamanho atual e aplicar o retângulo com margem.</summary>
    PseudoMaximize,

    /// <summary>Maximizou de novo a partir do estado pseudo-maximizado: voltar ao tamanho original.</summary>
    RestoreOriginal,
}

/// <summary>
/// Estado das janelas pseudo-maximizadas e a regra do "toggle". Não chama Win32: recebe os retângulos já lidos.
/// </summary>
public sealed class PseudoMaximizeTracker
{
    private readonly Dictionary<nint, TrackedWindow> _windows = [];

    public int Count => _windows.Count;

    public IReadOnlyCollection<TrackedWindow> Windows => [.. _windows.Values];

    public bool TryGet(nint hwnd, out TrackedWindow tracked) => _windows.TryGetValue(hwnd, out tracked!);

    public void Track(TrackedWindow window) => _windows[window.Hwnd] = window;

    public bool Forget(nint hwnd) => _windows.Remove(hwnd);

    public void Clear() => _windows.Clear();

    /// <summary>
    /// A janela acabou de maximizar. <paramref name="normalBeforeMaximize"/> é o <c>rcNormalPosition</c>
    /// atual, ou seja, o retângulo que ela tinha antes deste maximizar.
    /// </summary>
    public MaximizeAction OnMaximized(nint hwnd, PixelRect normalBeforeMaximize)
    {
        if (_windows.TryGetValue(hwnd, out var tracked) && tracked.PseudoNormalPosition.NearlyEquals(normalBeforeMaximize))
        {
            return MaximizeAction.RestoreOriginal;
        }

        // Nunca foi pseudo-maximizada, ou o usuário moveu/redimensionou nesse meio-tempo: maximizar novo.
        _windows.Remove(hwnd);
        return MaximizeAction.PseudoMaximize;
    }

    /// <summary>
    /// A janela mudou de posição sem estar maximizada. Devolve <c>true</c> se ela saiu do retângulo
    /// pseudo-maximizado (e por isso deixou de ser rastreada).
    /// </summary>
    /// <param name="hwnd">Janela.</param>
    /// <param name="normalPosition"><c>rcNormalPosition</c> atual.</param>
    /// <param name="windowRect">Retângulo de tela atual, ou <c>null</c> se a janela está minimizada.</param>
    public bool OnMovedWhileNotMaximized(nint hwnd, PixelRect normalPosition, PixelRect? windowRect)
    {
        if (!_windows.TryGetValue(hwnd, out var tracked))
        {
            return false;
        }

        var stillPseudo = tracked.PseudoNormalPosition.NearlyEquals(normalPosition) &&
                          (windowRect is not { } rect || tracked.PseudoWindowRect.NearlyEquals(rect));
        if (stillPseudo)
        {
            return false;
        }

        _windows.Remove(hwnd);
        return true;
    }
}
