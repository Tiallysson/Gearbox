using Gearbox.Core;
using Gearbox.Core.Settings;
using Gearbox.Interop.Desktop;
using Gearbox.Modules.Wallpaper.Logic;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gearbox.Modules.Wallpaper.Tests.Logic;

public sealed class WallpaperFitModeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gearbox-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData(WallpaperFitMode.Fill, WallpaperFit.Fill)]
    [InlineData(WallpaperFitMode.Fit, WallpaperFit.Fit)]
    [InlineData(WallpaperFitMode.Stretch, WallpaperFit.Stretch)]
    [InlineData(WallpaperFitMode.Center, WallpaperFit.Center)]
    public void ToWallpaperFit_MapeiaParaOAjusteDoWindows(WallpaperFitMode mode, WallpaperFit expected)
    {
        Assert.Equal(expected, mode.ToWallpaperFit());
    }

    [Fact]
    public void ToWallpaperFit_CobreTodosOsModos()
    {
        foreach (var mode in Enum.GetValues<WallpaperFitMode>())
        {
            Assert.True(Enum.IsDefined(mode.ToWallpaperFit()));
        }
    }

    [Theory]
    [InlineData("fill", WallpaperFitMode.Fill)]
    [InlineData("fit", WallpaperFitMode.Fit)]
    [InlineData("stretch", WallpaperFitMode.Stretch)]
    [InlineData("center", WallpaperFitMode.Center)]
    public void Load_ArquivoAntigo_MantemOAjuste(string json, WallpaperFitMode expected)
    {
        var paths = new GearboxPaths(_root);
        var service = new JsonSettingsService<WallpaperSettings>("Wallpaper", paths, NullLogger<JsonSettingsService<WallpaperSettings>>.Instance);
        Directory.CreateDirectory(Path.Combine(_root, "settings"));
        File.WriteAllText(Path.Combine(_root, "settings", "Wallpaper.json"), $$"""{ "fit": "{{json}}", "pauseOnBattery": false }""");

        var loaded = service.Load();

        Assert.Equal(expected, loaded.Fit);
        Assert.False(loaded.PauseOnBattery);
    }
}
