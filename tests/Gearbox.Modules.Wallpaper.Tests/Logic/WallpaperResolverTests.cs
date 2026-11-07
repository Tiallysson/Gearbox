using Gearbox.Modules.Wallpaper.Logic;
using Gearbox.Modules.Wallpaper.Logic.Gallery;

namespace Gearbox.Modules.Wallpaper.Tests.Logic;

public sealed class WallpaperResolverTests
{
    private const string Monitor1 = @"\\?\DISPLAY#AAA#1";
    private const string Monitor2 = @"\\?\DISPLAY#BBB#2";

    private static IReadOnlyList<WallpaperAssignment> Resolve(WallpaperSettings settings, params string[] existingFiles)
    {
        var files = new HashSet<string>(existingFiles, StringComparer.OrdinalIgnoreCase);
        return WallpaperResolver.Resolve(settings, [Monitor1, Monitor2], files.Contains);
    }

    [Fact]
    public void NadaEscolhido_MantemOOriginalSemAvisar()
    {
        var result = Resolve(new WallpaperSettings());

        Assert.All(result, a =>
        {
            Assert.True(a.KeepsOriginal);
            Assert.False(a.IsGallery);
            Assert.Equal(WallpaperProblem.None, a.Problem);
        });
    }

    [Fact]
    public void PadraoDasConfiguracoes_NenhumWallpaperDaGaleriaAtivo()
    {
        Assert.Null(new WallpaperSettings().GalleryWallpaperId);
    }

    [Fact]
    public void GaleriaInterativa_ViraCenaEmTodosOsMonitores()
    {
        var result = Resolve(new WallpaperSettings { GalleryWallpaperId = GalleryCatalog.Particles });

        Assert.All(result, a =>
        {
            Assert.Equal(MediaKind.Scene, a.Kind);
            Assert.Equal(GalleryCatalog.Particles, a.Path);
            Assert.Equal(GalleryCatalog.Particles, a.GalleryId);
        });
    }

    [Fact]
    public void GaleriaEstatica_UsaAImagemGeradaParaCadaMonitor()
    {
        var settings = new WallpaperSettings { GalleryWallpaperId = GalleryCatalog.Nature };

        var result = WallpaperResolver.Resolve(
            settings,
            [Monitor1, Monitor2],
            _ => true,
            galleryImage: (item, monitor) => monitor == Monitor1 ? GalleryImage.Ready($@"C:\cache\{item.Id}-1.png") : GalleryImage.Pending);

        Assert.Equal((@"C:\cache\nature-1.png", MediaKind.Image), (result[0].Path, result[0].Kind));
        Assert.True(result[1].IsPending);
        Assert.False(result[1].KeepsOriginal);
        Assert.Equal(GalleryCatalog.Nature, result[1].GalleryId);
        Assert.Equal(WallpaperProblem.None, result[1].Problem);
    }

    [Fact]
    public void GaleriaEstaticaSemGerador_FicaPendente()
    {
        var result = Resolve(new WallpaperSettings { GalleryWallpaperId = GalleryCatalog.Aurora });

        Assert.All(result, a =>
        {
            Assert.Equal(MediaKind.Pending, a.Kind);
            Assert.Null(a.Path);
            Assert.False(a.KeepsOriginal);
        });
    }

    [Fact]
    public void GaleriaEstaticaQueFalhou_MantemOOriginalEAvisa()
    {
        var settings = new WallpaperSettings { GalleryWallpaperId = GalleryCatalog.Geometric };

        var result = WallpaperResolver.Resolve(settings, [Monitor1], _ => true, galleryImage: (_, _) => GalleryImage.FailedToRender);

        var assignment = Assert.Single(result);
        Assert.True(assignment.KeepsOriginal);
        Assert.Equal(GalleryCatalog.Geometric, assignment.GalleryId);
        Assert.Equal(WallpaperProblem.RenderFailed, assignment.Problem);
    }

    [Fact]
    public void GaleriaAtiva_IgnoraVideosQueFalharam()
    {
        var settings = new WallpaperSettings
        {
            GalleryWallpaperId = GalleryCatalog.DigitalClock,
            Default = new MonitorWallpaper { Path = @"C:\w\loop.webm" },
        };
        var failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\w\loop.webm" };

        var result = WallpaperResolver.Resolve(settings, [Monitor1], _ => true, failed);

        var assignment = Assert.Single(result);
        Assert.Equal(MediaKind.Scene, assignment.Kind);
        Assert.Equal(WallpaperProblem.None, assignment.Problem);
    }

    [Fact]
    public void GaleriaAtiva_GanhaDosArquivosEscolhidos()
    {
        var settings = new WallpaperSettings
        {
            GalleryWallpaperId = GalleryCatalog.FluidWaves,
            Default = new MonitorWallpaper { Path = @"C:\w\praia.jpg" },
        };

        var result = Resolve(settings, @"C:\w\praia.jpg");

        Assert.All(result, a => Assert.Equal(MediaKind.Scene, a.Kind));
    }

    [Fact]
    public void GaleriaDesconhecida_VoltaParaOsArquivos()
    {
        var settings = new WallpaperSettings
        {
            GalleryWallpaperId = "nao-existe",
            Default = new MonitorWallpaper { Path = @"C:\w\praia.jpg" },
        };

        var result = Resolve(settings, @"C:\w\praia.jpg");

        Assert.All(result, a => Assert.Equal((@"C:\w\praia.jpg", MediaKind.Image, false), (a.Path, a.Kind, a.IsGallery)));
    }

    [Fact]
    public void MesmoEmTodos_UsaAEscolhaGeralEIgnoraAsPorMonitor()
    {
        var settings = new WallpaperSettings
        {
            SameForAllMonitors = true,
            Default = new MonitorWallpaper { Path = @"C:\w\loop.mp4" },
            Monitors = { [Monitor2] = new MonitorWallpaper { Path = @"C:\w\praia.jpg" } },
        };

        var result = Resolve(settings, @"C:\w\loop.mp4", @"C:\w\praia.jpg");

        Assert.All(result, a => Assert.Equal((@"C:\w\loop.mp4", MediaKind.Video), (a.Path, a.Kind)));
    }

    [Fact]
    public void PorMonitor_CadaUmComOSeu_ESemEscolhaHerdaOGeral()
    {
        var settings = new WallpaperSettings
        {
            SameForAllMonitors = false,
            Default = new MonitorWallpaper { Path = @"C:\w\geral.jpg" },
            Monitors = { [Monitor2] = new MonitorWallpaper { Path = @"C:\w\loop.mp4" } },
        };

        var result = Resolve(settings, @"C:\w\geral.jpg", @"C:\w\loop.mp4");

        Assert.Equal((@"C:\w\geral.jpg", MediaKind.Image), (result[0].Path, result[0].Kind));
        Assert.Equal((@"C:\w\loop.mp4", MediaKind.Video), (result[1].Path, result[1].Kind));
    }

    [Fact]
    public void PorMonitor_ChaveComOutraCaixa_AindaEncontra()
    {
        var settings = new WallpaperSettings
        {
            SameForAllMonitors = false,
            Monitors = { [Monitor1.ToLowerInvariant()] = new MonitorWallpaper { Path = @"C:\w\praia.jpg" } },
        };

        var result = Resolve(settings, @"C:\w\praia.jpg");

        Assert.Equal(@"C:\w\praia.jpg", result[0].Path);
        Assert.True(result[1].KeepsOriginal);
    }

    [Fact]
    public void ArquivoApagado_MantemOOriginalEAvisa()
    {
        var settings = new WallpaperSettings { Default = new MonitorWallpaper { Path = @"C:\w\sumiu.mp4" } };

        var result = Resolve(settings);

        Assert.All(result, a =>
        {
            Assert.True(a.KeepsOriginal);
            Assert.Equal(WallpaperProblem.FileNotFound, a.Problem);
        });
    }

    [Fact]
    public void FormatoNaoSuportado_MantemOOriginalEAvisa()
    {
        var settings = new WallpaperSettings { Default = new MonitorWallpaper { Path = @"C:\w\anim.gif" } };

        var result = Resolve(settings, @"C:\w\anim.gif");

        Assert.All(result, a =>
        {
            Assert.True(a.KeepsOriginal);
            Assert.Equal(WallpaperProblem.UnsupportedFormat, a.Problem);
        });
    }

    [Fact]
    public void VideoQueFalhou_MantemOOriginalEAvisa()
    {
        var settings = new WallpaperSettings { Default = new MonitorWallpaper { Path = @"C:\w\loop.webm" } };
        var failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\w\LOOP.webm" };

        var result = WallpaperResolver.Resolve(settings, [Monitor1], _ => true, failed);

        var assignment = Assert.Single(result);
        Assert.True(assignment.KeepsOriginal);
        Assert.Equal(WallpaperProblem.PlaybackFailed, assignment.Problem);
    }

    [Fact]
    public void MonitorNovo_HerdaAEscolhaGeral()
    {
        var settings = new WallpaperSettings
        {
            SameForAllMonitors = false,
            Default = new MonitorWallpaper { Path = @"C:\w\geral.jpg" },
            Monitors = { [Monitor1] = new MonitorWallpaper { Path = @"C:\w\praia.jpg" } },
        };

        var result = WallpaperResolver.Resolve(settings, [@"\\?\DISPLAY#NOVO#3"], _ => true);

        Assert.Equal(@"C:\w\geral.jpg", Assert.Single(result).Path);
    }
}
