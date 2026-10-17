using Darwin.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Darwin.Core.Modules;

/// <summary>Estado ligado/desligado de cada módulo, persistido em <c>modules.json</c>.</summary>
public sealed class ModuleStateSettings
{
    public Dictionary<string, bool> Enabled { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Liga, desliga e isola os módulos. Nenhuma exceção de módulo sai daqui: ela é logada,
/// o módulo é desligado e o estado "desligado" é persistido para não falhar de novo no próximo início.
/// </summary>
/// <remarks>Todos os métodos devem ser chamados na thread de UI.</remarks>
public sealed class ModuleManager : IDisposable
{
    private readonly ISettingsService<ModuleStateSettings> _settings;
    private readonly ModuleFaultHub _faultHub;
    private readonly ILogger<ModuleManager> _logger;
    private readonly Dictionary<string, Exception> _lastFaults = new(StringComparer.OrdinalIgnoreCase);

    public ModuleManager(
        IEnumerable<IDarwinModule> modules,
        ISettingsService<ModuleStateSettings> settings,
        ModuleFaultHub faultHub,
        ILogger<ModuleManager> logger)
    {
        _settings = settings;
        _faultHub = faultHub;
        _logger = logger;
        Modules = [.. modules];

        var duplicated = Modules.GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicated is not null)
        {
            throw new InvalidOperationException($"Há mais de um módulo registrado com o Id '{duplicated.Key}'.");
        }

        _faultHub.FaultReported += OnFaultReported;
    }

    public IReadOnlyList<IDarwinModule> Modules { get; }

    /// <summary>Disparado sempre que um módulo liga ou desliga (inclusive por falha).</summary>
    public event EventHandler<ModuleStateChangedEventArgs>? ModuleStateChanged;

    /// <summary>Última exceção que desligou o módulo (some quando ele volta a ligar com sucesso).</summary>
    public Exception? GetLastFault(IDarwinModule module) =>
        _lastFaults.TryGetValue(module.Id, out var fault) ? fault : null;

    public IDarwinModule? Find(string moduleId) =>
        Modules.FirstOrDefault(m => string.Equals(m.Id, moduleId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Liga os módulos que estavam ligados na última execução (novos módulos começam ligados).</summary>
    public void StartEnabledModules()
    {
        var states = _settings.Load();
        foreach (var module in Modules)
        {
            if (!states.Enabled.TryGetValue(module.Id, out var enabled) || enabled)
            {
                TryStart(module);
            }
        }
    }

    /// <summary>Liga ou desliga um módulo a pedido do usuário. Devolve o estado final.</summary>
    public bool SetEnabled(IDarwinModule module, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(module);

        if (module.IsEnabled != enabled)
        {
            if (enabled)
            {
                TryStart(module);
            }
            else
            {
                TryStop(module);
                RaiseChanged(module, fault: null);
            }
        }

        PersistState(module.Id, module.IsEnabled);
        return module.IsEnabled;
    }

    /// <summary>Desliga todos os módulos (sem persistir: no próximo início eles voltam como estavam).</summary>
    public void StopAll()
    {
        foreach (var module in Modules.Where(m => m.IsEnabled))
        {
            TryStop(module);
        }
    }

    public void Dispose() => _faultHub.FaultReported -= OnFaultReported;

    private void TryStart(IDarwinModule module)
    {
        try
        {
            _logger.LogInformation("Iniciando módulo {ModuleId}", module.Id);
            module.Start();
            _lastFaults.Remove(module.Id);
            RaiseChanged(module, fault: null);
        }
        catch (Exception ex)
        {
            Disable(module, ex);
        }
    }

    private bool TryStop(IDarwinModule module)
    {
        try
        {
            _logger.LogInformation("Parando módulo {ModuleId}", module.Id);
            module.Stop();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parar o módulo {ModuleId}", module.Id);
            return false;
        }
    }

    private void OnFaultReported(object? sender, ModuleFaultEventArgs e)
    {
        var module = Find(e.ModuleId);
        if (module is null)
        {
            _logger.LogError(e.Exception, "Falha reportada por módulo desconhecido {ModuleId}", e.ModuleId);
            return;
        }

        Disable(module, e.Exception);
    }

    private void Disable(IDarwinModule module, Exception fault)
    {
        _logger.LogError(fault, "O módulo {ModuleId} falhou e foi desabilitado", module.Id);
        _lastFaults[module.Id] = fault;

        if (module.IsEnabled)
        {
            TryStop(module);
        }

        PersistState(module.Id, enabled: false);
        RaiseChanged(module, fault);
    }

    private void PersistState(string moduleId, bool enabled)
    {
        try
        {
            var states = _settings.Load();
            states.Enabled[moduleId] = enabled;
            _settings.Save(states);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Não foi possível salvar o estado do módulo {ModuleId}", moduleId);
        }
    }

    private void RaiseChanged(IDarwinModule module, Exception? fault)
    {
        try
        {
            ModuleStateChanged?.Invoke(this, new ModuleStateChangedEventArgs(module, module.IsEnabled, fault));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro em um assinante de ModuleStateChanged");
        }
    }
}

public sealed class ModuleStateChangedEventArgs(IDarwinModule module, bool isEnabled, Exception? fault) : EventArgs
{
    public IDarwinModule Module { get; } = module;

    public bool IsEnabled { get; } = isEnabled;

    /// <summary>Preenchido quando o módulo foi desligado por causa de uma exceção.</summary>
    public Exception? Fault { get; } = fault;
}
