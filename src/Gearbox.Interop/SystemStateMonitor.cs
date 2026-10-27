using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Gearbox.Interop;

/// <summary>
/// Avisa quando a sessão é bloqueada, a tela apaga, os monitores mudam ou o Explorer reinicia.
/// </summary>
/// <remarks>
/// Usa uma janela de topo oculta (não <c>HWND_MESSAGE</c>): <c>WM_DISPLAYCHANGE</c> e <c>TaskbarCreated</c> são
/// broadcasts, que só chegam a janelas de topo. Precisa ser criado na thread de UI, cujo loop de mensagens
/// entrega os eventos; eles são disparados nessa mesma thread.
/// </remarks>
public sealed class SystemStateMonitor : IDisposable
{
    // O WndProc pertence à instância. Compartilhar o nome reutilizaria o callback da primeira janela.
    private readonly string _className = $"Gearbox.SystemStateMonitor.{Guid.NewGuid():N}";
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private bool _classRegistered;

    private readonly ILogger _logger;
    private readonly WNDPROC _wndProc; // Campo: o delegate não pode ser coletado enquanto a janela existir.
    private readonly uint _taskbarCreatedMessage;
    private HWND _hwnd;
    private HPOWERNOTIFY _displayNotification;
    private bool _sessionRegistered;

    public SystemStateMonitor(ILogger logger)
    {
        _logger = logger;
        _wndProc = WndProc;
        _taskbarCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarCreated");
        try
        {
            CreateWindow();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Sessão bloqueada (<c>true</c>) ou desbloqueada (<c>false</c>).</summary>
    public event EventHandler<bool>? SessionLockChanged;

    /// <summary>Tela ligada (<c>true</c>) ou apagada/esmaecida (<c>false</c>).</summary>
    public event EventHandler<bool>? DisplayPowerChanged;

    /// <summary>Monitores conectados, desconectados ou com resolução alterada.</summary>
    public event EventHandler? DisplaysChanged;

    /// <summary>O Explorer reiniciou (a camada da área de trabalho foi recriada).</summary>
    public event EventHandler? ExplorerRestarted;

    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _threadId)
        {
            throw new InvalidOperationException("SystemStateMonitor deve ser descartado na thread que o criou.");
        }

        if (_sessionRegistered)
        {
            PInvoke.WTSUnRegisterSessionNotification(_hwnd);
            _sessionRegistered = false;
        }

        if (!_displayNotification.IsNull)
        {
            PInvoke.UnregisterPowerSettingNotification(_displayNotification);
            _displayNotification = default;
        }

        if (!_hwnd.IsNull)
        {
            if (!PInvoke.DestroyWindow(_hwnd))
            {
                throw new InvalidOperationException($"DestroyWindow falhou ({Marshal.GetLastPInvokeError()}).");
            }

            _hwnd = HWND.Null;
        }

        if (_classRegistered)
        {
            using var module = PInvoke.GetModuleHandle((string?)null);
            if (!PInvoke.UnregisterClass(_className, module))
            {
                throw new InvalidOperationException($"UnregisterClass falhou ({Marshal.GetLastPInvokeError()}).");
            }

            _classRegistered = false;
        }
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

        _sessionRegistered = PInvoke.WTSRegisterSessionNotification(_hwnd, PInvoke.NOTIFY_FOR_THIS_SESSION);
        if (!_sessionRegistered)
        {
            _logger.LogWarning("WTSRegisterSessionNotification falhou ({Error}); o bloqueio de sessão não será detectado", Marshal.GetLastPInvokeError());
        }

        var displayGuid = PInvoke.GUID_CONSOLE_DISPLAY_STATE;
        _displayNotification = PInvoke.RegisterPowerSettingNotification(new HANDLE(_hwnd.Value), &displayGuid, REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE);
        if (_displayNotification.IsNull)
        {
            _logger.LogWarning("RegisterPowerSettingNotification falhou ({Error}); a tela apagada não será detectada", Marshal.GetLastPInvokeError());
        }
    }

    private unsafe LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            switch (msg)
            {
                case PInvoke.WM_WTSSESSION_CHANGE:
                    if (wParam.Value == PInvoke.WTS_SESSION_LOCK)
                    {
                        SessionLockChanged?.Invoke(this, true);
                    }
                    else if (wParam.Value == PInvoke.WTS_SESSION_UNLOCK)
                    {
                        SessionLockChanged?.Invoke(this, false);
                    }

                    break;

                case PInvoke.WM_POWERBROADCAST when wParam.Value == PInvoke.PBT_POWERSETTINGCHANGE:
                    var setting = (POWERBROADCAST_SETTING*)lParam.Value;
                    if (setting->PowerSetting == PInvoke.GUID_CONSOLE_DISPLAY_STATE && setting->DataLength >= sizeof(uint))
                    {
                        // 0 = apagada, 1 = ligada, 2 = esmaecida.
                        DisplayPowerChanged?.Invoke(this, *(uint*)&setting->Data == 1);
                    }

                    break;

                case PInvoke.WM_DISPLAYCHANGE:
                    DisplaysChanged?.Invoke(this, EventArgs.Empty);
                    break;

                default:
                    if (msg == _taskbarCreatedMessage && msg != 0)
                    {
                        ExplorerRestarted?.Invoke(this, EventArgs.Empty);
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            // Nunca deixar uma exceção escapar para o WndProc nativo (derrubaria o processo).
            _logger.LogError(ex, "Erro ao tratar a mensagem 0x{Message:X} do sistema", msg);
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}

