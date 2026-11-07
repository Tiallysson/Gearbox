using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Gearbox.Interop.Clipboard;

public sealed class ClipboardChangedEventArgs(bool isExcluded, uint sequenceNumber) : EventArgs
{
    public bool IsExcluded { get; } = isExcluded;

    public uint SequenceNumber { get; } = sequenceNumber;
}

public sealed class ClipboardMonitor : IDisposable
{
    private readonly string _className = $"Gearbox.ClipboardMonitor.{Guid.NewGuid():N}";
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private readonly ILogger _logger;
    private readonly WNDPROC _wndProc;
    private readonly uint _excludeFormat;
    private readonly uint _viewerIgnoreFormat;
    private readonly uint _historyFormat;
    private bool _classRegistered;
    private bool _listening;
    private HWND _hwnd;

    public ClipboardMonitor(ILogger logger)
    {
        _logger = logger;
        _wndProc = WndProc;
        _excludeFormat = PInvoke.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        _viewerIgnoreFormat = PInvoke.RegisterClipboardFormat("Clipboard Viewer Ignore");
        _historyFormat = PInvoke.RegisterClipboardFormat("CanIncludeInClipboardHistory");
        try
        {
            CreateWindow();
            _listening = PInvoke.AddClipboardFormatListener(_hwnd);
            if (!_listening)
            {
                throw new InvalidOperationException($"AddClipboardFormatListener falhou ({Marshal.GetLastPInvokeError()}).");
            }
        }
        catch
        {
            Release();
            throw;
        }
    }

    public event EventHandler<ClipboardChangedEventArgs>? Changed;

    public static uint SequenceNumber => PInvoke.GetClipboardSequenceNumber();

    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _threadId)
        {
            throw new InvalidOperationException("ClipboardMonitor deve ser descartado na thread que o criou.");
        }

        var failures = Release();
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", failures));
        }
    }

    private List<string> Release()
    {
        var failures = new List<string>();
        if (_listening)
        {
            if (!PInvoke.RemoveClipboardFormatListener(_hwnd))
            {
                failures.Add(LogFailure("RemoveClipboardFormatListener"));
            }

            _listening = false;
        }

        if (!_hwnd.IsNull)
        {
            if (!PInvoke.DestroyWindow(_hwnd))
            {
                failures.Add(LogFailure("DestroyWindow"));
            }

            _hwnd = HWND.Null;
        }

        if (_classRegistered)
        {
            using var module = PInvoke.GetModuleHandle((string?)null);
            if (!PInvoke.UnregisterClass(_className, module))
            {
                failures.Add(LogFailure("UnregisterClass"));
            }

            _classRegistered = false;
        }

        return failures;
    }

    private string LogFailure(string function)
    {
        var failure = $"{function} falhou ({Marshal.GetLastPInvokeError()}).";
        _logger.LogWarning("Falha ao liberar o monitor da área de transferência: {Failure}", failure);
        return failure;
    }

    private unsafe void CreateWindow()
    {
        using var module = PInvoke.GetModuleHandle((string?)null);
        var instance = new HINSTANCE(module.DangerousGetHandle());
        fixed (char* className = _className)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = _wndProc,
                hInstance = instance,
                lpszClassName = className,
            };

            if (PInvoke.RegisterClassEx(in wc) == 0)
            {
                throw new InvalidOperationException($"RegisterClassEx falhou ({Marshal.GetLastPInvokeError()}).");
            }

            _classRegistered = true;
        }

        _hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
            _className,
            string.Empty,
            WINDOW_STYLE.WS_POPUP,
            0,
            0,
            0,
            0,
            HWND.Null,
            null,
            module,
            null);

        if (_hwnd.IsNull)
        {
            throw new InvalidOperationException($"CreateWindowEx falhou ({Marshal.GetLastPInvokeError()}).");
        }
    }

    private LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == PInvoke.WM_CLIPBOARDUPDATE)
        {
            try
            {
                Changed?.Invoke(this, new ClipboardChangedEventArgs(IsExcluded(), SequenceNumber));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao tratar a mudança da área de transferência");
            }
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private bool IsExcluded() =>
        IsAvailable(_excludeFormat) || IsAvailable(_viewerIgnoreFormat) || IsHistoryDisallowed();

    private static bool IsAvailable(uint format) => format != 0 && PInvoke.IsClipboardFormatAvailable(format);

    private unsafe bool IsHistoryDisallowed()
    {
        if (!IsAvailable(_historyFormat))
        {
            return false;
        }

        if (!PInvoke.OpenClipboard(_hwnd))
        {
            return true;
        }

        try
        {
            var data = PInvoke.GetClipboardData(_historyFormat);
            if (data.IsNull)
            {
                return true;
            }

            var global = new HGLOBAL(data.Value);
            var value = PInvoke.GlobalLock(global);
            if (value is null)
            {
                return true;
            }

            try
            {
                return *(uint*)value == 0;
            }
            finally
            {
                PInvoke.GlobalUnlock(global);
            }
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }
}
