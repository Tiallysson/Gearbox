using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.WinRT;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Gearbox.Interop;

/// <summary>
/// Deixa o fundo Win32 de uma janela WinUI transparente, para que só o conteúdo XAML apareça (cantos arredondados,
/// formas que não são retângulos).
/// </summary>
/// <remarks>
/// <para>
/// Uma janela WinUI tem dois fundos: o da janela Win32 e o do backdrop do XAML. Este tira o primeiro: liga o
/// "blur behind" do DWM com uma região vazia (o DWM passa a respeitar o alfa da janela, sem desfocar nada) e
/// pinta o fundo de preto, que com o blur behind ligado vira transparente. O segundo é tirado por um
/// <c>SystemBackdrop</c> com um pincel transparente, que precisa de <see cref="EnsureSystemDispatcherQueue"/>.
/// </para>
/// <para>
/// A instância guarda o procedimento de janela substituto: precisa viver enquanto a janela existir.
/// </para>
/// </remarks>
public sealed class TransparentWindow
{
    // Campo: a fila do sistema só existe enquanto o controlador não for coletado.
    private static Windows.System.DispatcherQueueController? s_dispatcherController;

    private readonly WNDPROC _wndProc; // Campo: o Windows chama o delegate enquanto a janela existir.
    private readonly WNDPROC _previousWndProc;

    private TransparentWindow(nint hwnd)
    {
        _wndProc = WndProc;
        var previous = PInvoke.SetWindowLongPtr(
            new HWND(hwnd),
            WINDOW_LONG_PTR_INDEX.GWL_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProc));
        _previousWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(previous);
    }

    /// <summary>Aplica a transparência. Devolve <c>null</c> se o DWM recusar (a janela fica opaca).</summary>
    public static TransparentWindow? Apply(nint hwnd)
    {
        var region = PInvoke.CreateRectRgn(-2, -2, -1, -1);
        try
        {
            var blur = new DWM_BLURBEHIND
            {
                dwFlags = PInvoke.DWM_BB_ENABLE | PInvoke.DWM_BB_BLURREGION,
                fEnable = true,
                hRgnBlur = region,
            };
            if (PInvoke.DwmEnableBlurBehindWindow(new HWND(hwnd), in blur).Failed)
            {
                return null;
            }
        }
        finally
        {
            PInvoke.DeleteObject(region);
        }

        return new TransparentWindow(hwnd);
    }

    /// <summary>
    /// Garante um <c>Windows.System.DispatcherQueue</c> na thread atual, exigido pelo compositor do sistema
    /// (<c>Windows.UI.Composition</c>) que cria o pincel do backdrop. A thread de UI do WinUI só tem o
    /// <c>Microsoft.UI.Dispatching.DispatcherQueue</c>.
    /// </summary>
    public static void EnsureSystemDispatcherQueue()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() is not null)
        {
            return;
        }

        var options = new DispatcherQueueOptions
        {
            dwSize = (uint)Marshal.SizeOf<DispatcherQueueOptions>(),
            threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
            apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
        };
        PInvoke.CreateDispatcherQueueController(options, out s_dispatcherController).ThrowOnFailure();
    }

    private unsafe LRESULT WndProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        switch (message)
        {
            case PInvoke.WM_ERASEBKGND:
                return new LRESULT(1);

            case PInvoke.WM_PAINT:
                var hdc = PInvoke.BeginPaint(hwnd, out var paint);
                if (!hdc.IsNull)
                {
                    PInvoke.FillRect(hdc, &paint.rcPaint, new HBRUSH(PInvoke.GetStockObject(GET_STOCK_OBJECT_FLAGS.BLACK_BRUSH).Value));
                    PInvoke.EndPaint(hwnd, in paint);
                }

                return new LRESULT(0);

            default:
                return PInvoke.CallWindowProc(_previousWndProc, hwnd, message, wParam, lParam);
        }
    }
}
