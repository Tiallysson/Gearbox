using Gearbox.Core;
using Gearbox.Core.Settings;
using Gearbox.Interop;
using Gearbox.Modules.Wallpaper.Native;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gearbox.Modules.Wallpaper.Tests;

public sealed class WallpaperRestorationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gearbox-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeBackend _api = new();
    private readonly MemoryStore _store = new();

    [Fact]
    public void MonitorAdicionadoDepois_TambemTemSnapshotERestauracao()
    {
        var first = Original("A");
        var replacement = Image("gearbox.jpg");
        var restoration = Create();
        Assert.True(restoration.TryApplyImage("A", replacement));

        var second = Original("B");
        Assert.True(restoration.TryApplyImage("B", replacement));
        restoration.SetFit(WallpaperFit.Fill);
        restoration.RestoreAll(new[] { "A", "B" });

        Assert.Equal(first, _api.Images["A"]);
        Assert.Equal(second, _api.Images["B"]);
        Assert.Equal(WallpaperFit.Center, _api.Fit);
        Assert.True(_store.State.IsEmpty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("original-inexistente.jpg")]
    public void OriginalNaoRecuperavel_PreservaFundo(string? original)
    {
        _api.Images["A"] = original;

        Assert.False(Create().TryApplyImage("A", Image("gearbox.jpg")));

        Assert.Equal(original, _api.Images["A"]);
        Assert.Equal(0, _api.SetCalls);
        Assert.True(_store.State.IsEmpty);
    }

    [Fact]
    public void ApresentacaoDeSlides_PreservaConfiguracao()
    {
        var original = Original("A");
        _api.HasSlideshow = true;

        Assert.False(Create().TryApplyImage("A", Image("gearbox.jpg")));

        Assert.Equal(original, _api.Images["A"]);
        Assert.Equal(0, _api.SetCalls);
        Assert.True(_store.State.IsEmpty);
    }

    [Fact]
    public void MonitorDesconectado_MantemSnapshotAteReconectarEmOutraExecucao()
    {
        Original("A");
        var second = Original("B");
        var replacement = Image("gearbox.jpg");
        var restoration = Create();
        restoration.TryApplyImage("A", replacement);
        restoration.TryApplyImage("B", replacement);

        restoration.RestoreAll(new[] { "A" });

        Assert.Single(_store.State.Monitors);
        Assert.True(_store.State.Monitors.ContainsKey("B"));
        Assert.Equal(replacement, _api.Images["B"]);

        Create().RestoreAll(new[] { "A", "B" });

        Assert.Equal(second, _api.Images["B"]);
        Assert.True(_store.State.IsEmpty);
    }

    [Fact]
    public void FalhaEmUmMonitor_NaoImpedeOutroENaoDescartaSnapshot()
    {
        var first = Original("A");
        var second = Original("B");
        var replacement = Image("gearbox.jpg");
        var restoration = Create();
        restoration.TryApplyImage("A", replacement);
        restoration.TryApplyImage("B", replacement);
        _api.FailingMonitor = "A";

        restoration.RestoreAll(new[] { "A", "B" });

        Assert.Equal(second, _api.Images["B"]);
        Assert.Single(_store.State.Monitors);
        Assert.True(_store.State.Monitors.ContainsKey("A"));

        _api.FailingMonitor = null;
        Create().RestoreAll(new[] { "A", "B" });
        Assert.Equal(first, _api.Images["A"]);
        Assert.True(_store.State.IsEmpty);
    }

    [Fact]
    public void FalhaAoSalvarSnapshot_NaoAlteraWindows()
    {
        var original = Original("A");
        var restoration = Create();
        _store.FailSave = true;

        Assert.Throws<IOException>(() => restoration.TryApplyImage("A", Image("gearbox.jpg")));

        Assert.Equal(original, _api.Images["A"]);
        Assert.Equal(0, _api.SetCalls);
        Assert.True(_store.State.IsEmpty);
    }

    [Fact]
    public void OriginalApagado_UsaCopiaComMesmoConteudo()
    {
        var original = Original("A");
        var content = File.ReadAllText(original);
        var restoration = Create();
        restoration.TryApplyImage("A", Image("gearbox.jpg"));
        var backup = _store.State.Monitors["A"].BackupPath;
        File.Delete(original);

        restoration.RestoreAll(new[] { "A" });

        Assert.Equal(backup, _api.Images["A"]);
        Assert.Equal(content, File.ReadAllText(backup!));
        Assert.True(_store.State.IsEmpty);
    }

    [Fact]
    public void OriginalECopiaApagados_MantemRestauracaoPendente()
    {
        var original = Original("A");
        var restoration = Create();
        restoration.TryApplyImage("A", Image("gearbox.jpg"));
        File.Delete(original);
        File.Delete(_store.State.Monitors["A"].BackupPath!);

        restoration.RestoreAll(new[] { "A" });

        Assert.Single(_store.State.Monitors);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WallpaperRestoration Create() => new(_api, _store, new GearboxPaths(_root), NullLogger.Instance);

    private string Original(string monitor)
    {
        var path = Image($"original-{monitor}.jpg");
        _api.Images[monitor] = path;
        return path;
    }

    private string Image(string name)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, $"Conteúdo de {name}");
        return path;
    }

    private sealed class MemoryStore : ISettingsService<WallpaperRestoreState>
    {
        public WallpaperRestoreState State { get; private set; } = new();
        public bool FailSave { get; set; }
        public event EventHandler<WallpaperRestoreState>? Changed;
        public WallpaperRestoreState Load() => State;

        public void Save(WallpaperRestoreState settings)
        {
            if (FailSave)
            {
                throw new IOException("Gravação indisponível");
            }

            State = settings;
            Changed?.Invoke(this, settings);
        }
    }

    private sealed class FakeBackend : IDesktopWallpaperBackend
    {
        public Dictionary<string, string?> Images { get; } = [];
        public bool HasSlideshow { get; set; }
        public WallpaperFit Fit { get; private set; } = WallpaperFit.Center;
        public string? FailingMonitor { get; set; }
        public int SetCalls { get; private set; }
        public string? GetWallpaper(string devicePath) => Images.GetValueOrDefault(devicePath);
        public WallpaperFit GetFit() => Fit;
        public void SetFit(WallpaperFit fit) => Fit = fit;

        public void SetWallpaper(string devicePath, string imagePath)
        {
            SetCalls++;
            if (devicePath == FailingMonitor)
            {
                throw new InvalidOperationException("Monitor indisponível");
            }

            Images[devicePath] = imagePath;
        }
    }
}
