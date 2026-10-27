using Gearbox.Interop;
using Gearbox.Modules.RoundedMaximize.Logic;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.RoundedMaximize.Native;

internal interface IWindowPlacementApi
{
    int LastError { get; }
    bool TryGetPlacement(nint hwnd, out WindowPlacement placement);
    bool TryGetWorkArea(nint hwnd, out PixelRect workArea);
    bool TrySetPlacement(nint hwnd, WindowPlacement placement);
    FrameInsets GetFrameInsets(nint hwnd);
    bool TrySetBounds(nint hwnd, PixelRect bounds);
    bool TryGetWindowRect(nint hwnd, out PixelRect rect);
}

internal sealed class WindowPlacementApi : IWindowPlacementApi
{
    public int LastError => NativeWindow.LastError;
    public bool TryGetPlacement(nint hwnd, out WindowPlacement placement) => NativeWindow.TryGetPlacement(hwnd, out placement);
    public bool TryGetWorkArea(nint hwnd, out PixelRect workArea) => NativeWindow.TryGetWorkArea(hwnd, out workArea);
    public bool TrySetPlacement(nint hwnd, WindowPlacement placement) => NativeWindow.TrySetPlacement(hwnd, placement);
    public FrameInsets GetFrameInsets(nint hwnd) => NativeWindow.GetFrameInsets(hwnd);
    public bool TrySetBounds(nint hwnd, PixelRect bounds) => NativeWindow.TrySetBounds(hwnd, bounds);
    public bool TryGetWindowRect(nint hwnd, out PixelRect rect) => NativeWindow.TryGetWindowRect(hwnd, out rect);
}

internal sealed record PlacementAttempt(TrackedWindow? Window, string? FailedOperation = null, int Error = 0);

/// <summary>Uma aplicação parcial sempre tenta rollback; se ele falhar, o estado fica disponível para nova tentativa.</summary>
internal sealed class WindowPlacementTransaction(IWindowPlacementApi api, ILogger logger)
{
    private readonly Dictionary<nint, WindowPlacement> _pending = [];

    public bool HasPending(nint hwnd) => _pending.ContainsKey(hwnd);

    public void Forget(nint hwnd) => _pending.Remove(hwnd);

    public PlacementAttempt Apply(nint hwnd, int margin)
    {
        if (!api.TryGetPlacement(hwnd, out var original) || !api.TryGetWorkArea(hwnd, out var workArea))
        {
            return new PlacementAttempt(null);
        }

        _pending[hwnd] = original;
        var committed = false;
        try
        {
            if (!api.TrySetPlacement(hwnd, original with { ShowCommand = ShowWindowCommand.Normal }))
            {
                return new PlacementAttempt(null, "SetWindowPlacement", api.LastError);
            }

            var target = PseudoMaximizeLayout.Compute(workArea, margin, api.GetFrameInsets(hwnd));
            if (!api.TrySetBounds(hwnd, target))
            {
                return new PlacementAttempt(null, "SetWindowPos", api.LastError);
            }

            if (!api.TryGetPlacement(hwnd, out var pseudo))
            {
                return new PlacementAttempt(null, "GetWindowPlacement", api.LastError);
            }

            if (!api.TryGetWindowRect(hwnd, out var rect))
            {
                return new PlacementAttempt(null, "GetWindowRect", api.LastError);
            }

            committed = true;
            return new PlacementAttempt(new TrackedWindow(hwnd, original, pseudo.NormalPosition, rect));
        }
        finally
        {
            if (committed)
            {
                _pending.Remove(hwnd);
            }
            else
            {
                Restore(hwnd, original);
            }
        }
    }

    public void RestorePending()
    {
        foreach (var (hwnd, placement) in _pending.ToArray())
        {
            Restore(hwnd, placement);
        }
    }

    private void Restore(nint hwnd, WindowPlacement placement)
    {
        try
        {
            if (api.TrySetPlacement(hwnd, placement))
            {
                _pending.Remove(hwnd);
                return;
            }

            logger.LogWarning("Rollback da janela 0x{Hwnd:X} pendente (erro {Error})", hwnd, api.LastError);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rollback da janela 0x{Hwnd:X} pendente", hwnd);
        }
    }
}
