using Darwin.Core.Modules;
using Darwin.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darwin.Core.Tests;

public sealed class ModuleManagerTests
{
    private readonly InMemorySettings<ModuleStateSettings> _states = new();
    private readonly ModuleFaultHub _faultHub = new();

    [Fact]
    public void StartEnabledModules_ModuloNovo_ComecaLigado()
    {
        var module = new FakeModule("a");
        var manager = CreateManager(module);

        manager.StartEnabledModules();

        Assert.True(module.IsEnabled);
    }

    [Fact]
    public void StartEnabledModules_RespeitaEstadoSalvo()
    {
        _states.Value.Enabled["a"] = false;
        var module = new FakeModule("a");
        var manager = CreateManager(module);

        manager.StartEnabledModules();

        Assert.False(module.IsEnabled);
        Assert.Equal(0, module.StartCount);
    }

    [Fact]
    public void Start_QueLancaExcecao_NaoPropagaDesabilitaEPersiste()
    {
        var broken = new FakeModule("broken") { ThrowOnStart = true };
        var healthy = new FakeModule("healthy");
        var manager = CreateManager(broken, healthy);
        ModuleStateChangedEventArgs? faultEvent = null;
        manager.ModuleStateChanged += (_, e) => faultEvent = e.Fault is not null ? e : faultEvent;

        manager.StartEnabledModules();

        Assert.False(broken.IsEnabled);
        Assert.True(healthy.IsEnabled);
        Assert.False(_states.Value.Enabled["broken"]);
        Assert.NotNull(faultEvent);
        Assert.Same(broken, faultEvent.Module);
        Assert.NotNull(manager.GetLastFault(broken));
    }

    [Fact]
    public void ReportFault_ParaOModuloEPersisteDesligado()
    {
        var module = new FakeModule("a");
        var manager = CreateManager(module);
        manager.StartEnabledModules();

        _faultHub.ReportFault("a", new InvalidOperationException("boom"));

        Assert.False(module.IsEnabled);
        Assert.Equal(1, module.StopCount);
        Assert.False(_states.Value.Enabled["a"]);
        Assert.Equal("boom", manager.GetLastFault(module)?.Message);
    }

    [Fact]
    public void ReportFault_ModuloDesconhecido_NaoLanca()
    {
        CreateManager(new FakeModule("a"));

        var exception = Record.Exception(() => _faultHub.ReportFault("nao-existe", new InvalidOperationException()));

        Assert.Null(exception);
    }

    [Fact]
    public void Stop_QueLancaExcecao_NaoPropaga()
    {
        var module = new FakeModule("a") { ThrowOnStop = true };
        var manager = CreateManager(module);
        manager.StartEnabledModules();

        var exception = Record.Exception(() => manager.SetEnabled(module, false));

        Assert.Null(exception);
    }

    [Fact]
    public void SetEnabled_LigaDesligaEPersiste()
    {
        _states.Value.Enabled["a"] = false;
        var module = new FakeModule("a");
        var manager = CreateManager(module);
        manager.StartEnabledModules();

        Assert.True(manager.SetEnabled(module, true));
        Assert.True(_states.Value.Enabled["a"]);

        Assert.False(manager.SetEnabled(module, false));
        Assert.False(_states.Value.Enabled["a"]);
    }

    [Fact]
    public void SetEnabled_ReligarComSucesso_LimpaAFalha()
    {
        var module = new FakeModule("a") { ThrowOnStart = true };
        var manager = CreateManager(module);
        manager.StartEnabledModules();

        module.ThrowOnStart = false;
        manager.SetEnabled(module, true);

        Assert.Null(manager.GetLastFault(module));
    }

    [Fact]
    public void StopAll_NaoAlteraEstadoPersistido()
    {
        var module = new FakeModule("a");
        var manager = CreateManager(module);
        manager.StartEnabledModules();
        var savesBefore = _states.SaveCount;

        manager.StopAll();

        Assert.False(module.IsEnabled);
        Assert.Equal(savesBefore, _states.SaveCount);
    }

    [Fact]
    public void Construtor_IdsDuplicados_Lanca()
    {
        Assert.Throws<InvalidOperationException>(() => CreateManager(new FakeModule("a"), new FakeModule("A")));
    }

    private ModuleManager CreateManager(params IDarwinModule[] modules) =>
        new(modules, _states, _faultHub, NullLogger<ModuleManager>.Instance);

    private sealed class FakeModule(string id) : IDarwinModule
    {
        public string Id => id;

        public string DisplayName => id;

        public string Description => string.Empty;

        public string IconGlyph => string.Empty;

        public bool IsEnabled { get; private set; }

        public Type SettingsPageType => typeof(object);

        public bool ThrowOnStart { get; set; }

        public bool ThrowOnStop { get; set; }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public void Start()
        {
            StartCount++;
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("falha no Start");
            }

            IsEnabled = true;
        }

        public void Stop()
        {
            StopCount++;
            IsEnabled = false;
            if (ThrowOnStop)
            {
                throw new InvalidOperationException("falha no Stop");
            }
        }
    }

    private sealed class InMemorySettings<T> : ISettingsService<T>
        where T : class, new()
    {
        public event EventHandler<T>? Changed;

        public T Value { get; private set; } = new();

        public int SaveCount { get; private set; }

        public T Load() => Value;

        public void Save(T settings)
        {
            Value = settings;
            SaveCount++;
            Changed?.Invoke(this, settings);
        }
    }
}
