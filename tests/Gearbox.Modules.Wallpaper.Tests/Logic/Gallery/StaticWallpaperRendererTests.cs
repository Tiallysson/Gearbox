using Gearbox.Modules.Wallpaper.Logic.Gallery;

namespace Gearbox.Modules.Wallpaper.Tests.Logic.Gallery;

public sealed class StaticWallpaperRendererTests
{
    public static TheoryData<string> StaticIds => new(GalleryCatalog.All.Where(i => i.Kind == GalleryKind.Static).Select(i => i.Id));

    [Theory]
    [MemberData(nameof(StaticIds))]
    public void Render_PreencheTodosOsPixelsOpacos(string id)
    {
        var pixels = StaticWallpaperRenderer.Render(id, 160, 90);

        Assert.Equal(160 * 90 * 4, pixels.Length);
        Assert.All(Enumerable.Range(0, 160 * 90), i => Assert.Equal(255, pixels[(i * 4) + 3]));
    }

    [Theory]
    [MemberData(nameof(StaticIds))]
    public void Render_EDeterministico(string id)
    {
        Assert.Equal(StaticWallpaperRenderer.Render(id, 96, 54), StaticWallpaperRenderer.Render(id, 96, 54));
    }

    [Theory]
    [MemberData(nameof(StaticIds))]
    public void Render_TemVariedadeDeCor(string id)
    {
        var pixels = StaticWallpaperRenderer.Render(id, 160, 90);
        var luma = Enumerable.Range(0, 160 * 90).Select(i => (pixels[(i * 4) + 2] + pixels[(i * 4) + 1] + pixels[i * 4]) / 3).ToList();

        Assert.True(luma.Max() - luma.Min() > 25);
    }

    [Fact]
    public void Render_CadaWallpaperEDiferente()
    {
        var renders = GalleryCatalog.All
            .Where(i => i.Kind == GalleryKind.Static)
            .Select(i => Convert.ToHexString(StaticWallpaperRenderer.Render(i.Id, 64, 36)))
            .ToList();

        Assert.Equal(renders.Count, renders.Distinct().Count());
    }

    [Theory]
    [InlineData(GalleryCatalog.Particles)]
    [InlineData("nao-existe")]
    public void Render_SoAceitaEstaticos(string id)
    {
        Assert.Throws<ArgumentException>(() => StaticWallpaperRenderer.Render(id, 10, 10));
    }

    [Theory]
    [MemberData(nameof(StaticIds))]
    public void Render_TamanhosMinimosNaoQuebram(string id)
    {
        Assert.Equal(4, StaticWallpaperRenderer.Render(id, 1, 1).Length);
        Assert.Equal(2 * 3 * 4, StaticWallpaperRenderer.Render(id, 2, 3).Length);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, -1)]
    public void Render_TamanhoInvalido_Recusa(int width, int height)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => StaticWallpaperRenderer.Render(GalleryCatalog.Aurora, width, height));
    }

    [Fact]
    public void Render_ProporcaoUltrawideEVertical()
    {
        Assert.Equal(344 * 144 * 4, StaticWallpaperRenderer.Render(GalleryCatalog.Nature, 344, 144).Length);
        Assert.Equal(90 * 160 * 4, StaticWallpaperRenderer.Render(GalleryCatalog.Geometric, 90, 160).Length);
    }
}
