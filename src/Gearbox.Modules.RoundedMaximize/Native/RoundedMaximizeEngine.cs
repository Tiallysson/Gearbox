using Gearbox.Interop;
using Gearbox.Modules.RoundedMaximize.Logic;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.RoundedMaximize.Native;

/// <summary>
/// Aplica o "falso maximizar" nas janelas reais. As decisões ficam nas classes de <c>Logic</c>;
/// aqui só se lê o estado das janelas e se chama o Win32/DWM.
/// </summary>
/// <remarks>Usado apenas na thread de UI (a mesma do hook de eventos).</remarks>
internal sealed class RoundedMaximizeEngine(ILogger logger)
{
    private readonly PseudoMaximizeTracker _tracker = new();
    private readonly WindowPlacementTransaction _placement = new(new WindowPlacementApi(), logger);
    private readonly ProcessNameCache _processNames = new(hwnd => NativeWindow.GetProcessName(NativeWindow.GetProcessId(hwnd)));
    private readonly HashSet<nint> _failedWindows = [];
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;

    private EligibilityRules _rules = new((uint)Environment.ProcessId, []);
    private int _margin = RoundedMaximizeSettings.DefaultMargin;
    private uint _borderColor = NativeWindow.DefaultBorderColor;
    private bool _busy;

    public int TrackedCount => _tracker.Count;

    /// <summary>Aplica novas configurações, inclusive às janelas que já estão pseudo-maximizadas.</summary>
    public void Configure(int margin, uint borderColor, IEnumerable<string> excludedProcesses)
    {
        margin = PseudoMaximizeLayout.ClampMargin(margin);
        var marginChanged = margin != _margin;
        var colorChanged = borderColor != _borderColor;

        _margin = margin;
        _borderColor = borderColor;
        _rules = new EligibilityRules(_ownProcessId, excludedProcesses);

        foreach (var tracked in _tracker.Windows)
        {
            if (_rules.IsExcluded(_processNames.Get(tracked.Hwnd)))
            {
                logger.LogInformation("Janela {Hwnd} agora está excluída; restaurando", Hex(tracked.Hwnd));
                RestoreOriginal(tracked, activate: false);
                continue;
            }

            if (marginChanged)
            {
                Relayout(tracked);
            }

            if (colorChanged)
            {
                ApplyBorderColor(tracked.Hwnd);
            }
        }
    }

    public void OnWinEvent(WinEventArgs e)
    {
        switch (e.Kind)
        {
            case WinEventKind.LocationChanged:
                OnLocationChanged(e.Hwnd);
                break;
            case WinEventKind.Destroyed:
                _tracker.Forget(e.Hwnd);
                _placement.Forget(e.Hwnd);
                _processNames.Remove(e.Hwnd);
                _failedWindows.Remove(e.Hwnd);
                break;
        }
    }

    /// <summary>Devolve todas as janelas ao tamanho original e reverte os atributos do DWM.</summary>
    public void RestoreAll()
    {
        foreach (var tracked in _tracker.Windows)
        {
            try
            {
                if (NativeWindow.TryGetPlacement(tracked.Hwnd, out var placement) &&
                    placement.ShowCommand != ShowWindowCommand.ShowMaximized &&
                    tracked.PseudoNormalPosition.NearlyEquals(placement.NormalPosition))
                {
                    RestoreOriginal(tracked, activate: false);
                }
                else
                {
                    RevertDwm(tracked.Hwnd);
                }
            }
            catch (Exception ex)
            {
                // Uma janela problemática não pode impedir a restauração das outras.
                logger.LogWarning(ex, "Falha ao restaurar a janela {Hwnd}", Hex(tracked.Hwnd));
            }
        }

        _busy = true;
        try
        {
            _placement.RestorePending();
        }
        finally
        {
            _busy = false;
        }

        logger.LogInformation("{Count} janela(s) restaurada(s)", _tracker.Count);
        _tracker.Clear();
        _processNames.Clear();
        _failedWindows.Clear();
    }

    private void OnLocationChanged(nint hwnd)
    {
        // Nossas próprias chamadas a SetWindowPlacement/SetWindowPos geram eventos: ignorar.
        if (_busy || _placement.HasPending(hwnd))
        {
            return;
        }

        if (_tracker.TryGet(hwnd, out var tracked))
        {
            OnTrackedWindowChanged(tracked);
            return;
        }

        // Caminho rápido: a imensa maioria dos eventos é de janelas que não estão maximizadas.
        if (!NativeWindow.IsMaximized(hwnd) || _failedWindows.Contains(hwnd))
        {
            return;
        }

        var snapshot = Capture(hwnd);
        var eligibility = _rules.Evaluate(snapshot);
        if (eligibility != Eligibility.Eligible)
        {
            logger.LogTrace("Janela {Hwnd} ({Process}) ignorada: {Reason}", Hex(hwnd), snapshot.ProcessName, eligibility);
            return;
        }

        PseudoMaximize(hwnd, snapshot.ProcessName);
    }

    private void OnTrackedWindowChanged(TrackedWindow tracked)
    {
        var hwnd = tracked.Hwnd;
        if (!NativeWindow.TryGetPlacement(hwnd, out var placement))
        {
            _tracker.Forget(hwnd);
            return;
        }

        if (NativeWindow.IsMaximized(hwnd))
        {
            if (_tracker.OnMaximized(hwnd, placement.NormalPosition) == MaximizeAction.RestoreOriginal)
            {
                logger.LogDebug("Janela {Hwnd} maximizada de novo: voltando ao tamanho original", Hex(hwnd));
                RestoreOriginal(tracked, activate: true);
            }
            else
            {
                RevertDwm(hwnd);
                PseudoMaximize(hwnd, _processNames.Get(hwnd));
            }

            return;
        }

        PixelRect? windowRect = null;
        if (!NativeWindow.IsMinimized(hwnd) && NativeWindow.TryGetWindowRect(hwnd, out var rect))
        {
            windowRect = rect;
        }

        if (_tracker.OnMovedWhileNotMaximized(hwnd, placement.NormalPosition, windowRect))
        {
            logger.LogDebug("Janela {Hwnd} foi movida/redimensionada: deixou de ser pseudo-maximizada", Hex(hwnd));
            RevertDwm(hwnd);
        }
    }

    private void PseudoMaximize(nint hwnd, string? processName)
    {
        _busy = true;
        try
        {
            var result = _placement.Apply(hwnd, _margin);
            if (result.Window is not { } tracked)
            {
                if (result.FailedOperation is { } operation)
                {
                    MarkFailed(hwnd, processName, operation, result.Error);
                }

                return;
            }

            _tracker.Track(tracked);
            ApplyDwm(hwnd);
            logger.LogDebug("Janela {Hwnd} ({Process}) pseudo-maximizada em {Rect}", Hex(hwnd), processName, tracked.PseudoWindowRect);
        }
        finally
        {
            _busy = false;
        }
    }

    private void RestoreOriginal(TrackedWindow tracked, bool activate)
    {
        var hwnd = tracked.Hwnd;
        _busy = true;
        try
        {
            _tracker.Forget(hwnd);
            RevertDwm(hwnd);

            if (!NativeWindow.TryGetPlacement(hwnd, out var current))
            {
                return;
            }

            var command = activate
                ? ShowWindowCommand.Normal
                : NativeWindow.IsMinimized(hwnd) ? ShowWindowCommand.ShowMinNoActive : ShowWindowCommand.ShowNoActivate;

            var restored = current with
            {
                ShowCommand = command,
                NormalPosition = tracked.OriginalPlacement.NormalPosition,
            };

            if (!NativeWindow.TrySetPlacement(hwnd, restored))
            {
                logger.LogWarning("Não foi possível restaurar a janela {Hwnd} (erro {Error})", Hex(hwnd), NativeWindow.LastError);
                return;
            }

            // Alguns frameworks (ex.: WinForms) reaplicam por conta própria o último tamanho "normal" que
            // conheciam, que é o pseudo-maximizado. Se isso aconteceu, insiste com SetWindowPos.
            if (!NativeWindow.IsMinimized(hwnd) &&
                NativeWindow.TryGetWindowRect(hwnd, out var actual) &&
                !actual.NearlyEquals(tracked.OriginalWindowRect))
            {
                logger.LogDebug("Janela {Hwnd} não voltou ao tamanho original ({Actual}); reaplicando", Hex(hwnd), actual);
                NativeWindow.TrySetBounds(hwnd, tracked.OriginalWindowRect);
            }
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Recalcula o retângulo de uma janela já pseudo-maximizada (margem mudou).</summary>
    private void Relayout(TrackedWindow tracked)
    {
        var hwnd = tracked.Hwnd;
        if (NativeWindow.IsMinimized(hwnd) || NativeWindow.IsMaximized(hwnd) ||
            !NativeWindow.TryGetWorkArea(hwnd, out var workArea))
        {
            return;
        }

        _busy = true;
        try
        {
            var target = PseudoMaximizeLayout.Compute(workArea, _margin, NativeWindow.GetFrameInsets(hwnd));
            if (NativeWindow.TrySetBounds(hwnd, target) &&
                NativeWindow.TryGetPlacement(hwnd, out var pseudo) &&
                NativeWindow.TryGetWindowRect(hwnd, out var pseudoRect))
            {
                _tracker.Track(tracked with { PseudoNormalPosition = pseudo.NormalPosition, PseudoWindowRect = pseudoRect });
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private WindowSnapshot Capture(nint hwnd)
    {
        var styles = NativeWindow.GetStyles(hwnd);
        return new WindowSnapshot(
            hwnd,
            NativeWindow.IsVisible(hwnd),
            NativeWindow.IsTopLevel(hwnd),
            styles.HasCaption,
            styles.HasMaximizeBox,
            styles.IsToolWindow,
            NativeWindow.IsCloaked(hwnd),
            NativeWindow.GetProcessId(hwnd),
            _processNames.Get(hwnd));
    }

    private void ApplyDwm(nint hwnd)
    {
        var hr = NativeWindow.SetCornerPreference(hwnd, CornerPreference.Round);
        if (hr < 0)
        {
            logger.LogWarning("DWMWA_WINDOW_CORNER_PREFERENCE falhou na janela {Hwnd}: HRESULT 0x{Hr:X8}", Hex(hwnd), hr);
        }

        ApplyBorderColor(hwnd);
    }

    private void ApplyBorderColor(nint hwnd)
    {
        var hr = NativeWindow.SetBorderColor(hwnd, _borderColor);
        if (hr < 0)
        {
            logger.LogWarning("DWMWA_BORDER_COLOR falhou na janela {Hwnd}: HRESULT 0x{Hr:X8}", Hex(hwnd), hr);
        }
    }

    private void RevertDwm(nint hwnd)
    {
        var hr1 = NativeWindow.SetCornerPreference(hwnd, CornerPreference.Default);
        var hr2 = NativeWindow.SetBorderColor(hwnd, NativeWindow.DefaultBorderColor);
        if (hr1 < 0 || hr2 < 0)
        {
            logger.LogDebug("Reverter DWM na janela {Hwnd}: 0x{Hr1:X8} / 0x{Hr2:X8}", Hex(hwnd), hr1, hr2);
        }
    }

    private void MarkFailed(nint hwnd, string? processName, string operation, int error)
    {
        // Típico de janelas elevadas (UIPI): logar uma vez e não tentar de novo até a janela ser destruída.
        _failedWindows.Add(hwnd);
        logger.LogWarning(
            "{Operation} falhou na janela {Hwnd} ({Process}), erro {Error}. Se for um app elevado, o Gearbox precisa rodar como administrador.",
            operation,
            Hex(hwnd),
            processName,
            error);
    }

    private static string Hex(nint hwnd) => $"0x{hwnd:X}";
}

