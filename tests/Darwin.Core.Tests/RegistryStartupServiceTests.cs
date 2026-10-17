using Darwin.Core.Startup;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darwin.Core.Tests;

public sealed class RegistryStartupServiceTests
{
    private const string ExePath = @"C:\Apps\Darwin\Darwin.exe";

    private readonly FakeRunKeyStore _store = new();

    [Fact]
    public void SetEnabled_True_GravaComandoComAspasEArgumento()
    {
        var service = CreateService();

        service.SetEnabled(true);

        Assert.True(service.IsEnabled);
        Assert.Equal($"\"{ExePath}\" --startup", _store.Values[RegistryStartupService.ValueName]);
    }

    [Fact]
    public void SetEnabled_False_RemoveOValor()
    {
        var service = CreateService();
        service.SetEnabled(true);

        service.SetEnabled(false);

        Assert.False(service.IsEnabled);
        Assert.False(_store.Values.ContainsKey(RegistryStartupService.ValueName));
    }

    [Fact]
    public void RefreshPathIfEnabled_CaminhoAntigo_Atualiza()
    {
        _store.Values[RegistryStartupService.ValueName] = "\"D:\\Velho\\Darwin.exe\" --startup";

        CreateService().RefreshPathIfEnabled();

        Assert.Equal($"\"{ExePath}\" --startup", _store.Values[RegistryStartupService.ValueName]);
    }

    [Fact]
    public void RefreshPathIfEnabled_Desligado_NaoCriaValor()
    {
        CreateService().RefreshPathIfEnabled();

        Assert.Empty(_store.Values);
    }

    private RegistryStartupService CreateService() =>
        new(_store, ExePath, NullLogger<RegistryStartupService>.Instance);

    private sealed class FakeRunKeyStore : IRunKeyStore
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? GetValue(string name) => Values.TryGetValue(name, out var value) ? value : null;

        public void SetValue(string name, string value) => Values[name] = value;

        public void DeleteValue(string name) => Values.Remove(name);
    }
}
