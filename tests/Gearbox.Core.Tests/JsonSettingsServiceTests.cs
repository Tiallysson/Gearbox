using Gearbox.Core.Modules;
using Gearbox.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gearbox.Core.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gearbox-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Load_SemArquivo_DevolvePadroes()
    {
        var service = CreateService();

        var settings = service.Load();

        Assert.Equal(42, settings.Number);
        Assert.Equal(["a"], settings.Items);
    }

    [Fact]
    public void Save_GravaNoCaminhoEsperado_ELoadDevolveOsValores()
    {
        var service = CreateService();

        service.Save(new SampleSettings { Number = 7, Items = ["x", "y"], Mode = SampleMode.Second });
        var loaded = service.Load();

        Assert.True(File.Exists(Path.Combine(_root, "settings", "sample.json")));
        Assert.Equal(7, loaded.Number);
        Assert.Equal(["x", "y"], loaded.Items);
        Assert.Equal(SampleMode.Second, loaded.Mode);
    }

    [Fact]
    public void Save_ListaGravadaSubstituiOPadrao()
    {
        var service = CreateService();

        service.Save(new SampleSettings { Items = [] });

        Assert.Empty(service.Load().Items);
    }

    [Fact]
    public void Save_DisparaChangedComUmaCopia()
    {
        var service = CreateService();
        SampleSettings? received = null;
        service.Changed += (_, s) => received = s;
        var original = new SampleSettings { Number = 3 };

        service.Save(original);

        Assert.NotNull(received);
        Assert.Equal(3, received.Number);
        Assert.NotSame(original, received);
    }

    [Fact]
    public void Load_ArquivoCorrompido_DevolvePadroesEGuardaCopia()
    {
        var service = CreateService();
        Directory.CreateDirectory(Path.Combine(_root, "settings"));
        File.WriteAllText(service.FilePath, "{ isto não é json");

        var settings = service.Load();

        Assert.Equal(42, settings.Number);
        Assert.True(File.Exists(service.FilePath + ".corrupt"));
    }

    [Fact]
    public void Json_UsaCamelCaseEEnumComoTexto()
    {
        var service = CreateService();

        service.Save(new SampleSettings { Mode = SampleMode.Second });
        var json = File.ReadAllText(service.FilePath);

        Assert.Contains("\"number\"", json, StringComparison.Ordinal);
        Assert.Contains("\"second\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"items\":null}")]
    [InlineData("{\"mode\":1234}")]
    [InlineData("{\"mode\":\"naoExiste\"}")]
    public void Load_JsonValidoComValorInvalido_RecuperaPadroes(string json)
    {
        var service = CreateService();
        Directory.CreateDirectory(Path.GetDirectoryName(service.FilePath)!);
        File.WriteAllText(service.FilePath, json);

        var loaded = service.Load();

        Assert.Equal(["a"], loaded.Items);
        Assert.Equal(SampleMode.First, loaded.Mode);
        Assert.Equal(json, File.ReadAllText(service.FilePath + ".corrupt"));
    }

    [Fact]
    public void Load_ModulosComEnabledNulo_RecuperaDicionario()
    {
        var service = new JsonSettingsService<ModuleStateSettings>(
            "modules", new GearboxPaths(_root), NullLogger<JsonSettingsService<ModuleStateSettings>>.Instance);
        Directory.CreateDirectory(Path.GetDirectoryName(service.FilePath)!);
        File.WriteAllText(service.FilePath, "{\"enabled\":null}");

        var loaded = service.Load();

        Assert.NotNull(loaded.Enabled);
        Assert.Empty(loaded.Enabled);
        Assert.True(File.Exists(service.FilePath + ".corrupt"));
    }

    [Fact]
    public void Save_ConfiguracaoInvalida_NaoSobrescreveArquivoValido()
    {
        var service = CreateService();
        service.Save(new SampleSettings { Number = 7 });
        var previous = File.ReadAllText(service.FilePath);

        Assert.Throws<System.Text.Json.JsonException>(() => service.Save(new SampleSettings { Items = null! }));

        Assert.Equal(previous, File.ReadAllText(service.FilePath));
    }

    private JsonSettingsService<SampleSettings> CreateService() =>
        new("sample", new GearboxPaths(_root), NullLogger<JsonSettingsService<SampleSettings>>.Instance);

    public enum SampleMode
    {
        First,
        Second,
    }

    public sealed class SampleSettings
    {
        public int Number { get; set; } = 42;

        public List<string> Items { get; set; } = ["a"];

        public SampleMode Mode { get; set; }
    }
}

