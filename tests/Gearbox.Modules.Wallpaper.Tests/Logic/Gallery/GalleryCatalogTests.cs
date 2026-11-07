using Gearbox.Modules.Wallpaper.Logic.Gallery;

namespace Gearbox.Modules.Wallpaper.Tests.Logic.Gallery;

public sealed class GalleryCatalogTests
{
    [Fact]
    public void All_TemCincoEstaticosECincoInterativosComIdsUnicos()
    {
        Assert.Equal(5, GalleryCatalog.All.Count(i => i.Kind == GalleryKind.Static));
        Assert.Equal(5, GalleryCatalog.All.Count(i => i.Kind == GalleryKind.Interactive));
        Assert.Equal(GalleryCatalog.All.Count, GalleryCatalog.All.Select(i => i.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData(GalleryFilter.All, 10)]
    [InlineData(GalleryFilter.Static, 5)]
    [InlineData(GalleryFilter.Interactive, 5)]
    public void Filter_SeparaPorTipo(GalleryFilter filter, int expected)
    {
        var items = GalleryCatalog.Filter(filter);

        Assert.Equal(expected, items.Count);
        if (filter == GalleryFilter.Interactive)
        {
            Assert.All(items, i => Assert.True(i.IsInteractive));
        }
    }

    [Theory]
    [InlineData("aurora", true)]
    [InlineData("AURORA-FLOW", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("nao-existe", false)]
    public void Find_SemDiferenciarMaiusculas(string? id, bool found)
    {
        Assert.Equal(found, GalleryCatalog.Find(id) is not null);
    }

    [Fact]
    public void CacheFileName_IncluiIdEResolucao()
    {
        var item = GalleryCatalog.Find(GalleryCatalog.Nature)!;

        Assert.Equal("nature-2560x1440.png", GalleryCatalog.CacheFileName(item, 2560, 1440));
    }
}
