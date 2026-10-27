using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Gearbox.Interop;

/// <summary>Preferência de cantos do DWM (Windows 11).</summary>
public enum CornerPreference
{
    Default = 0,
    DoNotRound = 1,
    Round = 2,
    RoundSmall = 3,
}

/// <summary>Estilos de uma janela já traduzidos para booleanos.</summary>
public readonly record struct WindowStyleInfo(bool HasCaption, bool HasMaximizeBox, bool IsToolWindow);

/// <summary>Resumo de uma janela de topo (para quem precisa varrer todas, como o wallpaper).</summary>
public readonly record struct TopLevelWindow(
    nint Hwnd,
    PixelRect Bounds,
    string ClassName,
    bool IsMinimized,
    bool IsCloaked,
    bool IsToolWindow,
    bool IsClickThrough,
    uint ProcessId);

/// <summary>Um monitor: área inteira, área de trabalho (sem a barra de tarefas) e escala de DPI (1.0 = 96 DPI).</summary>
public readonly record struct DisplayMonitor(PixelRect Bounds, PixelRect WorkArea, double DpiScale);

/// <summary>O que o Windows acha que o usuário está fazendo (mesmos valores de <c>QUERY_USER_NOTIFICATION_STATE</c>).</summary>
public enum UserNotificationState
{
    Unknown = 0,
    NotPresent = 1,
    Busy = 2,
    RunningDirect3DFullScreen = 3,
    PresentationMode = 4,
    AcceptsNotifications = 5,
    QuietTime = 6,
    App = 7,
}

/// <summary>Wrappers finos e tipados sobre o Win32/DWM. Nenhum tipo do CsWin32 vaza daqui.</summary>
public static class NativeWindow
{
    /// <summary>Valor de <c>DWMWA_BORDER_COLOR</c> que devolve a borda ao padrão do sistema.</summary>
    public const uint DefaultBorderColor = PInvoke.DWMWA_COLOR_DEFAULT;

    /// <summary>Último erro Win32 da thread (válido logo após uma chamada que falhou).</summary>
    public static int LastError => Marshal.GetLastPInvokeError();

    public static bool IsVisible(nint hwnd) => PInvoke.IsWindowVisible(new HWND(hwnd));

    public static bool IsMaximized(nint hwnd) => PInvoke.IsZoomed(new HWND(hwnd));

    public static bool IsMinimized(nint hwnd) => PInvoke.IsIconic(new HWND(hwnd));

    public static bool IsTopLevel(nint hwnd) => PInvoke.GetAncestor(new HWND(hwnd), GET_ANCESTOR_FLAGS.GA_ROOT) == new HWND(hwnd);

    public static nint GetForegroundWindow() => PInvoke.GetForegroundWindow();

    /// <summary>Posição do cursor em coordenadas de tela (pixels físicos).</summary>
    public static bool TryGetCursorPosition(out int x, out int y)
    {
        if (PInvoke.GetCursorPos(out var point))
        {
            x = point.X;
            y = point.Y;
            return true;
        }

        x = y = 0;
        return false;
    }

    public static bool AllowSetForegroundWindow(uint processId) => PInvoke.AllowSetForegroundWindow(processId);

    /// <summary>Escala de DPI da janela (1.0 = 96 DPI).</summary>
    public static double GetDpiScale(nint hwnd)
    {
        var dpi = PInvoke.GetDpiForWindow(new HWND(hwnd));
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    public static WindowStyleInfo GetStyles(nint hwnd)
    {
        var style = (WINDOW_STYLE)(uint)PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var exStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        return new WindowStyleInfo(
            HasCaption: (style & WINDOW_STYLE.WS_CAPTION) == WINDOW_STYLE.WS_CAPTION,
            HasMaximizeBox: (style & WINDOW_STYLE.WS_MAXIMIZEBOX) != 0,
            IsToolWindow: (exStyle & WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0);
    }

    public static string GetClassName(nint hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        var length = PInvoke.GetClassName(new HWND(hwnd), buffer);
        return length > 0 ? buffer[..length].ToString() : string.Empty;
    }

    /// <summary>Janelas de topo visíveis, da frente para trás (ordem Z).</summary>
    public static IReadOnlyList<TopLevelWindow> EnumerateTopLevelWindows()
    {
        var windows = new List<TopLevelWindow>();
        PInvoke.EnumWindows(
            (hwnd, _) =>
            {
                nint handle = hwnd;
                if (!PInvoke.IsWindowVisible(hwnd))
                {
                    // A maioria das janelas de topo é invisível: nem entram na lista.
                    return true;
                }

                var exStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
                TryGetWindowRect(handle, out var bounds);
                windows.Add(new TopLevelWindow(
                    handle,
                    bounds,
                    GetClassName(handle),
                    IsMinimized: PInvoke.IsIconic(hwnd),
                    IsCloaked: IsCloaked(handle),
                    IsToolWindow: (exStyle & WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0,
                    IsClickThrough: (exStyle & WINDOW_EX_STYLE.WS_EX_TRANSPARENT) != 0,
                    ProcessId: GetProcessId(handle)));
                return true;
            },
            0);

        return windows;
    }

    /// <summary>Janelas "cloaked" existem mas não aparecem (outras áreas de trabalho virtuais, apps UWP suspensos).</summary>
    public static unsafe bool IsCloaked(nint hwnd)
    {
        uint cloaked = 0;
        var hr = PInvoke.DwmGetWindowAttribute(new HWND(hwnd), DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
        return hr.Succeeded && cloaked != 0;
    }

    public static uint GetProcessId(nint hwnd)
    {
        _ = PInvoke.GetWindowThreadProcessId(new HWND(hwnd), out var processId);
        return processId;
    }

    /// <summary>Nome do executável sem extensão (ex.: <c>notepad</c>), ou <c>null</c> se não der para ler.</summary>
    public static string? GetProcessName(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (!process.IsInvalid)
        {
            Span<char> buffer = stackalloc char[1024];
            var size = (uint)buffer.Length;
            if (PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref size))
            {
                return Path.GetFileNameWithoutExtension(buffer[..(int)size].ToString());
            }
        }

        // Plano B (processos protegidos): enumeração do sistema, que não precisa de handle.
        try
        {
            using var managed = System.Diagnostics.Process.GetProcessById((int)processId);
            return managed.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public static bool TryGetPlacement(nint hwnd, out WindowPlacement placement)
    {
        var native = new WINDOWPLACEMENT { length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!PInvoke.GetWindowPlacement(new HWND(hwnd), ref native))
        {
            placement = default;
            return false;
        }

        placement = new WindowPlacement(
            (ShowWindowCommand)(uint)native.showCmd,
            ToPixelRect(native.rcNormalPosition),
            (uint)native.flags,
            native.ptMinPosition.X,
            native.ptMinPosition.Y,
            native.ptMaxPosition.X,
            native.ptMaxPosition.Y);
        return true;
    }

    public static bool TrySetPlacement(nint hwnd, WindowPlacement placement)
    {
        var native = new WINDOWPLACEMENT
        {
            length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>(),
            flags = (WINDOWPLACEMENT_FLAGS)placement.Flags,
            showCmd = (SHOW_WINDOW_CMD)(uint)placement.ShowCommand,
            ptMinPosition = new System.Drawing.Point(placement.MinPositionX, placement.MinPositionY),
            ptMaxPosition = new System.Drawing.Point(placement.MaxPositionX, placement.MaxPositionY),
            rcNormalPosition = ToRect(placement.NormalPosition),
        };
        return PInvoke.SetWindowPlacement(new HWND(hwnd), in native);
    }

    public static bool TryGetWindowRect(nint hwnd, out PixelRect rect)
    {
        if (PInvoke.GetWindowRect(new HWND(hwnd), out var native))
        {
            rect = ToPixelRect(native);
            return true;
        }

        rect = default;
        return false;
    }

    /// <summary>Área de trabalho (sem a barra de tarefas) do monitor mais próximo da janela, em coordenadas de tela.</summary>
    public static bool TryGetWorkArea(nint hwnd, out PixelRect workArea)
    {
        var monitor = PInvoke.MonitorFromWindow(new HWND(hwnd), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (monitor.IsNull || !PInvoke.GetMonitorInfo(monitor, ref info))
        {
            workArea = default;
            return false;
        }

        workArea = ToPixelRect(info.rcWork);
        return true;
    }

    /// <summary>Área de trabalho (sem a barra de tarefas) do monitor que mais se sobrepõe a <paramref name="area"/>.</summary>
    public static bool TryGetWorkAreaForRect(PixelRect area, out PixelRect workArea)
    {
        var rect = ToRect(area);
        var monitor = PInvoke.MonitorFromRect(rect, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (monitor.IsNull || !PInvoke.GetMonitorInfo(monitor, ref info))
        {
            workArea = default;
            return false;
        }

        workArea = ToPixelRect(info.rcWork);
        return true;
    }

    /// <summary>Monitor principal (o que contém a origem da tela).</summary>
    public static bool TryGetPrimaryMonitor(out DisplayMonitor monitor)
    {
        var handle = PInvoke.MonitorFromPoint(new System.Drawing.Point(0, 0), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (handle.IsNull || !PInvoke.GetMonitorInfo(handle, ref info))
        {
            monitor = default;
            return false;
        }

        var scale = PInvoke.GetDpiForMonitor(handle, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Succeeded && dpiX > 0
            ? dpiX / 96.0
            : 1.0;
        monitor = new DisplayMonitor(ToPixelRect(info.rcMonitor), ToPixelRect(info.rcWork), scale);
        return true;
    }

    /// <summary>Estado do usuário segundo o shell (detecta jogos em tela cheia exclusiva e modo apresentação).</summary>
    public static UserNotificationState GetUserNotificationState() =>
        PInvoke.SHQueryUserNotificationState(out var state).Succeeded ? (UserNotificationState)(int)state : UserNotificationState.Unknown;

    /// <summary>
    /// Transforma a janela num overlay: popup sem nenhuma moldura (a área cliente é a janela inteira), que não
    /// ativa ao ser clicado (não rouba o foco do app atual) e não aparece no Alt+Tab nem na barra de tarefas.
    /// </summary>
    /// <remarks>
    /// Mesmo sem borda e sem barra de título, o <c>OverlappedPresenter</c> do WinUI deixa um <c>WS_DLGFRAME</c>
    /// de 3 px em volta da área cliente. Chame depois de configurar o presenter.
    /// </remarks>
    public static void MakeOverlay(nint hwnd)
    {
        var handle = new HWND(hwnd);
        var style = (WINDOW_STYLE)(uint)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_THICKFRAME | WINDOW_STYLE.WS_SYSMENU |
            WINDOW_STYLE.WS_MINIMIZEBOX | WINDOW_STYLE.WS_MAXIMIZEBOX);
        style |= WINDOW_STYLE.WS_POPUP;
        _ = PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)(uint)style);

        var exStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
        exStyle &= ~(WINDOW_EX_STYLE.WS_EX_APPWINDOW | WINDOW_EX_STYLE.WS_EX_WINDOWEDGE |
            WINDOW_EX_STYLE.WS_EX_DLGMODALFRAME | WINDOW_EX_STYLE.WS_EX_CLIENTEDGE);
        _ = PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)(uint)exStyle);

        // Sem SWP_FRAMECHANGED o Windows continua usando a moldura antiga.
        PInvoke.SetWindowPos(
            handle,
            HWND.Null,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// Diferença entre <c>GetWindowRect</c> e a moldura visível (<c>DWMWA_EXTENDED_FRAME_BOUNDS</c>).
    /// No Windows 10/11 as bordas de redimensionamento são invisíveis e ocupam ~7 px por lado.
    /// </summary>
    public static unsafe FrameInsets GetFrameInsets(nint hwnd)
    {
        if (!TryGetWindowRect(hwnd, out var window))
        {
            return FrameInsets.None;
        }

        RECT frame;
        var hr = PInvoke.DwmGetWindowAttribute(new HWND(hwnd), DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &frame, (uint)sizeof(RECT));
        if (hr.Failed)
        {
            return FrameInsets.None;
        }

        return new FrameInsets(
            Math.Max(0, frame.left - window.Left),
            Math.Max(0, frame.top - window.Top),
            Math.Max(0, window.Right - frame.right),
            Math.Max(0, window.Bottom - frame.bottom));
    }

    /// <summary>Move/redimensiona sem mudar a ordem Z nem ativar a janela.</summary>
    public static bool TrySetBounds(nint hwnd, PixelRect bounds) =>
        PInvoke.SetWindowPos(
            new HWND(hwnd),
            HWND.Null,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

    /// <summary>Define <c>DWMWA_WINDOW_CORNER_PREFERENCE</c>. Devolve o HRESULT.</summary>
    public static unsafe int SetCornerPreference(nint hwnd, CornerPreference preference)
    {
        var value = (DWM_WINDOW_CORNER_PREFERENCE)(int)preference;
        return PInvoke.DwmSetWindowAttribute(new HWND(hwnd), DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &value, (uint)sizeof(DWM_WINDOW_CORNER_PREFERENCE)).Value;
    }

    /// <summary>Define <c>DWMWA_BORDER_COLOR</c> (COLORREF 0x00BBGGRR). Devolve o HRESULT.</summary>
    public static unsafe int SetBorderColor(nint hwnd, uint colorRef)
    {
        var value = colorRef;
        return PInvoke.DwmSetWindowAttribute(new HWND(hwnd), DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, &value, sizeof(uint)).Value;
    }

    private static PixelRect ToPixelRect(RECT rect) => new(rect.left, rect.top, rect.right, rect.bottom);

    private static RECT ToRect(PixelRect rect) => new() { left = rect.Left, top = rect.Top, right = rect.Right, bottom = rect.Bottom };
}
