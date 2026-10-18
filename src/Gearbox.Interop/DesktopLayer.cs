using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Gearbox.Interop;

/// <summary>
/// Camada atrás dos ícones da área de trabalho, onde o Explorer desenha o wallpaper.
/// Janelas presas aqui aparecem como wallpaper e não cobrem os ícones.
/// </summary>
public static class DesktopLayer
{
    /// <summary>Mensagem não documentada que faz o <c>Progman</c> criar o <c>WorkerW</c> do wallpaper.</summary>
    private const uint SpawnWorkerWMessage = 0x052C;

    /// <summary>
    /// Devolve a janela que deve ser pai dos wallpapers, ou 0 se o Explorer não estiver rodando.
    /// Cobre os dois layouts: o do Windows 11 24H2+ (WorkerW filho do Progman) e o clássico
    /// (WorkerW de topo, logo depois do que contém o <c>SHELLDLL_DefView</c>).
    /// </summary>
    public static unsafe nint FindHost()
    {
        var progman = PInvoke.FindWindow("Progman", null);
        if (progman.IsNull)
        {
            return 0;
        }

        _ = PInvoke.SendMessageTimeout(progman, SpawnWorkerWMessage, new WPARAM(0xD), new LPARAM(0x1), SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_NORMAL, 1000, null);

        var worker = PInvoke.FindWindowEx(progman, HWND.Null, "WorkerW", null);
        if (!worker.IsNull)
        {
            return worker;
        }

        var found = HWND.Null;
        PInvoke.EnumWindows(
            (top, _) =>
            {
                if (PInvoke.FindWindowEx(top, HWND.Null, "SHELLDLL_DefView", null).IsNull)
                {
                    return true;
                }

                found = PInvoke.FindWindowEx(HWND.Null, top, "WorkerW", null);
                return false;
            },
            0);

        return found.IsNull ? progman : found;
    }

    /// <summary>Prende <paramref name="hwnd"/> como filha de <paramref name="host"/>, cobrindo <paramref name="screenBounds"/>.</summary>
    public static bool Attach(nint hwnd, nint host, PixelRect screenBounds)
    {
        var window = new HWND(hwnd);
        var style = (WINDOW_STYLE)(uint)PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_THICKFRAME | WINDOW_STYLE.WS_SYSMENU | WINDOW_STYLE.WS_MINIMIZEBOX | WINDOW_STYLE.WS_MAXIMIZEBOX);
        style |= WINDOW_STYLE.WS_CHILD | WINDOW_STYLE.WS_VISIBLE;
        _ = PInvoke.SetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)style);

        if (PInvoke.SetParent(window, new HWND(host)).IsNull)
        {
            return false;
        }

        return SetBounds(hwnd, host, screenBounds);
    }

    /// <summary>Reposiciona uma janela já presa (coordenadas de tela convertidas para o cliente do host).</summary>
    public static unsafe bool SetBounds(nint hwnd, nint host, PixelRect screenBounds)
    {
        var points = stackalloc System.Drawing.Point[2];
        points[0] = new System.Drawing.Point(screenBounds.Left, screenBounds.Top);
        points[1] = new System.Drawing.Point(screenBounds.Right, screenBounds.Bottom);
        _ = PInvoke.MapWindowPoints(HWND.Null, new HWND(host), points, 2);

        return PInvoke.SetWindowPos(
            new HWND(hwnd),
            HWND.Null,
            points[0].X,
            points[0].Y,
            points[1].X - points[0].X,
            points[1].Y - points[0].Y,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
    }
}
