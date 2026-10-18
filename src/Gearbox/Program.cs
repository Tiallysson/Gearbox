using Gearbox.Interop;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Gearbox;

public static class Program
{
    /// <summary>Pede para a instância em execução sair de forma limpa (usado pelo instalador).</summary>
    public const string ExitArgument = "--exit";

    /// <summary>Chave da instância única (por usuário/sessão).</summary>
    private const string InstanceKey = "Gearbox.SingleInstance.6C1F0E2A";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (RedirectToRunningInstance())
        {
            return 0;
        }

        // "--exit" sem nenhuma instância rodando: não há o que encerrar.
        if (HasExitArgument(args))
        {
            return 0;
        }

        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }

    /// <summary>Procura <see cref="ExitArgument"/> numa lista de argumentos ou numa linha de comando inteira.</summary>
    public static bool HasExitArgument(IEnumerable<string> args) =>
        args.Any(a => string.Equals(a.Trim('"'), ExitArgument, StringComparison.OrdinalIgnoreCase));

    public static bool HasExitArgument(string? commandLine) =>
        commandLine is not null && HasExitArgument(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Se já existe um Gearbox rodando, entrega a ativação para ele (que mostra a janela ou encerra, com
    /// <c>--exit</c>) e devolve <c>true</c> para esta instância terminar.
    /// </summary>
    private static bool RedirectToRunningInstance()
    {
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (mainInstance.IsCurrent)
        {
            return false;
        }

        // Permite que a instância principal traga a janela para frente (senão o Windows só pisca na barra).
        NativeWindow.AllowSetForegroundWindow(mainInstance.ProcessId);

        var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

        // Fora da thread STA, para não bloquear o COM enquanto espera.
        Task.Run(() => mainInstance.RedirectActivationToAsync(activationArgs).AsTask()).Wait();
        return true;
    }
}
