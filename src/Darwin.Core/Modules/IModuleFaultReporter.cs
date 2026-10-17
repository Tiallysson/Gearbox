namespace Darwin.Core.Modules;

/// <summary>
/// Canal para um módulo reportar uma exceção que aconteceu fora de <see cref="IDarwinModule.Start"/>
/// e <see cref="IDarwinModule.Stop"/> (callbacks, eventos, timers). O casco loga e desabilita o módulo.
/// </summary>
public interface IModuleFaultReporter
{
    void ReportFault(string moduleId, Exception exception);
}

/// <summary>
/// Implementação padrão: apenas repassa a falha para quem estiver escutando (o <see cref="ModuleManager"/>).
/// Existe separada do gerenciador para não criar dependência circular módulo → gerenciador → módulos.
/// </summary>
public sealed class ModuleFaultHub : IModuleFaultReporter
{
    public event EventHandler<ModuleFaultEventArgs>? FaultReported;

    public void ReportFault(string moduleId, Exception exception) =>
        FaultReported?.Invoke(this, new ModuleFaultEventArgs(moduleId, exception));
}

public sealed class ModuleFaultEventArgs(string moduleId, Exception exception) : EventArgs
{
    public string ModuleId { get; } = moduleId;

    public Exception Exception { get; } = exception;
}
