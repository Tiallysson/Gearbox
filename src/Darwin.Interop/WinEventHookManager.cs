using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Darwin.Interop;

/// <summary>Eventos de janela distribuídos pelo <see cref="WinEventHookManager"/>.</summary>
public enum WinEventKind
{
    /// <summary><c>EVENT_OBJECT_LOCATIONCHANGE</c>: a janela moveu, redimensionou, maximizou ou restaurou.</summary>
    LocationChanged,

    /// <summary><c>EVENT_OBJECT_DESTROY</c>: a janela foi destruída.</summary>
    Destroyed,
}

public sealed class WinEventArgs(WinEventKind kind, nint hwnd) : EventArgs
{
    public WinEventKind Kind { get; } = kind;

    public nint Hwnd { get; } = hwnd;
}

public sealed class WinEventHandlerFaultedEventArgs(string ownerId, Exception exception) : EventArgs
{
    /// <summary>Quem assinou (normalmente o Id do módulo).</summary>
    public string OwnerId { get; } = ownerId;

    public Exception Exception { get; } = exception;
}

/// <summary>
/// Um único <c>SetWinEventHook</c> out-of-context compartilhado por todos os módulos.
/// </summary>
/// <remarks>
/// <para>
/// Precisa ser criado e usado na thread de UI: hooks out-of-context entregam os eventos pelo loop de
/// mensagens da thread que os instalou, então os assinantes são chamados nessa mesma thread.
/// </para>
/// <para>
/// O hook só é instalado enquanto houver assinantes. Cada assinante é isolado: se ele lançar uma exceção,
/// os demais continuam recebendo eventos e <see cref="HandlerFaulted"/> é disparado com o dono.
/// </para>
/// </remarks>
public sealed class WinEventHookManager : IDisposable
{
    private const int ObjIdWindow = (int)OBJECT_IDENTIFIER.OBJID_WINDOW;
    private const int ChildIdSelf = 0;

    private readonly ILogger<WinEventHookManager> _logger;
    private readonly int _threadId = Environment.CurrentManagedThreadId;

    // Mantém o delegate vivo enquanto o hook existir: o código nativo guarda só um ponteiro de função
    // e não impede o GC de coletar o delegate.
    private readonly WINEVENTPROC _callback;

    private Subscription[] _subscriptions = [];
    private UnhookWinEventSafeHandle? _hook;
    private bool _disposed;

    public WinEventHookManager(ILogger<WinEventHookManager> logger)
    {
        _logger = logger;
        _callback = OnWinEvent;
    }

    /// <summary>Um assinante lançou uma exceção ao tratar um evento.</summary>
    public event EventHandler<WinEventHandlerFaultedEventArgs>? HandlerFaulted;

    public bool IsHookInstalled => _hook is not null;

    /// <summary>
    /// Assina os eventos de janela (todos os tipos de <see cref="WinEventKind"/>).
    /// Só chegam eventos de janelas top-level de outros processos (<c>OBJID_WINDOW</c>, <c>CHILDID_SELF</c>).
    /// Descarte o retorno para cancelar a assinatura.
    /// </summary>
    public IDisposable Subscribe(string ownerId, EventHandler<WinEventArgs> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        ArgumentNullException.ThrowIfNull(handler);
        EnsureUiThread();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var subscription = new Subscription(this, ownerId, handler);
        _subscriptions = [.. _subscriptions, subscription];
        EnsureHook();
        return subscription;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscriptions = [];
        RemoveHook();
    }

    private void Unsubscribe(Subscription subscription)
    {
        EnsureUiThread();
        _subscriptions = [.. _subscriptions.Where(s => !ReferenceEquals(s, subscription))];
        if (_subscriptions.Length == 0)
        {
            RemoveHook();
        }
    }

    private void EnsureHook()
    {
        if (_hook is not null)
        {
            return;
        }

        // Faixa DESTROY..LOCATIONCHANGE: um único hook cobre os dois eventos de que precisamos.
        var hook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_OBJECT_DESTROY,
            PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
            null,
            _callback,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);

        if (hook.IsInvalid)
        {
            hook.Dispose();
            throw new InvalidOperationException("SetWinEventHook falhou.");
        }

        _hook = hook;

        _logger.LogDebug("Hook de eventos de janela instalado");
    }

    private void RemoveHook()
    {
        if (_hook is null)
        {
            return;
        }

        // O SafeHandle chama UnhookWinEvent ao ser descartado.
        _hook.Dispose();
        _hook = null;
        _logger.LogDebug("Hook de eventos de janela removido");
    }

    private void OnWinEvent(HWINEVENTHOOK hook, uint eventType, HWND hwnd, int idObject, int idChild, uint threadId, uint time)
    {
        if (hwnd.IsNull || idObject != ObjIdWindow || idChild != ChildIdSelf)
        {
            return;
        }

        WinEventKind kind;
        if (eventType == PInvoke.EVENT_OBJECT_LOCATIONCHANGE)
        {
            kind = WinEventKind.LocationChanged;
        }
        else if (eventType == PInvoke.EVENT_OBJECT_DESTROY)
        {
            kind = WinEventKind.Destroyed;
        }
        else
        {
            return;
        }

        var args = new WinEventArgs(kind, hwnd);

        // Copia do array: um assinante pode cancelar a assinatura (ou ser desligado) durante a distribuição.
        foreach (var subscription in _subscriptions)
        {
            if (subscription.IsActive)
            {
                subscription.Invoke(args);
            }
        }
    }

    private void OnHandlerFaulted(string ownerId, Exception exception)
    {
        _logger.LogError(exception, "Assinante {OwnerId} falhou ao tratar um evento de janela", ownerId);
        try
        {
            HandlerFaulted?.Invoke(this, new WinEventHandlerFaultedEventArgs(ownerId, exception));
        }
        catch (Exception ex)
        {
            // Nunca deixar uma exceção escapar para o callback nativo.
            _logger.LogError(ex, "Erro ao notificar falha do assinante {OwnerId}", ownerId);
        }
    }

    private void EnsureUiThread()
    {
        if (Environment.CurrentManagedThreadId != _threadId)
        {
            throw new InvalidOperationException("WinEventHookManager deve ser usado na thread que o criou (thread de UI).");
        }
    }

    private sealed class Subscription(WinEventHookManager owner, string ownerId, EventHandler<WinEventArgs> handler) : IDisposable
    {
        public bool IsActive { get; private set; } = true;

        public void Invoke(WinEventArgs args)
        {
            try
            {
                handler(owner, args);
            }
            catch (Exception ex)
            {
                owner.OnHandlerFaulted(ownerId, ex);
            }
        }

        public void Dispose()
        {
            if (IsActive)
            {
                IsActive = false;
                owner.Unsubscribe(this);
            }
        }
    }
}
