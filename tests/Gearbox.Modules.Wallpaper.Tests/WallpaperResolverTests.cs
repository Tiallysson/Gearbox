using Gearbox.Modules.Wallpaper.Logic;

namespace Gearbox.Modules.Wallpaper.Tests;

public sealed class WallpaperResolverTests
{
    private const string Monitor1 = @"\\?\DISPLAY#AAA#1";
    private const string Monitor2 = @"\\?\DISPLAY#BBB#2";

    private static readonly GearboxWallpaper s_builtInImageOnly = new(@"C:\Gearbox\Assets\Wallpapers\Gearbox.jpg", null);
    private static readonly GearboxWallpaper s_builtInWithVideo = new(@"C:\Gearbox\Assets\Wallpapers\Gearbox.jpg", @"C:\Gearbox\Assets\Wallpapers\Gearbox.mp4");

    private static IReadOnlyList<WallpaperAssignment> Resolve(WallpaperSettings settings, GearboxWallpaper builtIn, params string[] existingFiles)
    {
        var files = new HashSet<string>(existingFiles, StringComparer.OrdinalIgnoreCase) { builtIn.ImagePath };
        return WallpaperResolver.Resolve(settings, [Monitor1, Monitor2], builtIn, files.Contains);
    }

    [Fact]
    public void NadaEscolhido_UsaOWallpaperDoGearboxEmTodos()
    {
        var result = Resolve(new WallpaperSettings(), s_builtInImageOnly);

        Assert.All(result, a =>
        {
            Assert.True(a.IsGearboxWallpaper);
            Assert.Equal(MediaKind.Image, a.Kind);
            Assert.Equal(s_builtInImageOnly.ImagePath, a.Path);
            Assert.Equal(WallpaperProblem.None, a.Problem);
        });
    }

    [Fact]
    public void VideoEmbutidoExistente_TemPreferenciaSobreAImagem()
    {
        var result = Resolve(new WallpaperSettings(), s_builtInWithVideo, s_builtInWithVideo.VideoPath!);

        Assert.All(result, a => Assert.Equal((s_builtInWithVideo.VideoPath, MediaKind.Video), (a.Path, a.Kind)));
    }

    [Fact]
    public void VideoEmbutidoAusente_CaiNaImagem()
    {
        var result = Resolve(new WallpaperSettings(), s_builtInWithVideo);

        Assert.All(result, a => Assert.Equal((s_builtInWithVideo.ImagePath, MediaKind.Image), (a.Path, a.Kind)));
    }

    [Fact]
    public void SemWallpaperDoGearbox_ENadaEscolhido_MantemOOriginal()
    {
        var result = Resolve(new WallpaperSettings { UseGearboxWallpaper = false }, s_builtInImageOnly);

        Assert.All(result, a =>
        {
            Assert.True(a.KeepsOriginal);
            Assert.False(a.IsGearboxWallpaper);
        });
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

        var result = Resolve(settings, s_builtInImageOnly, @"C:\w\loop.mp4", @"C:\w\praia.jpg");

        Assert.All(result, a => Assert.Equal((@"C:\w\loop.mp4", MediaKind.Video, false), (a.Path, a.Kind, a.IsGearboxWallpaper)));
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

        var result = Resolve(settings, s_builtInImageOnly, @"C:\w\geral.jpg", @"C:\w\loop.mp4");

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

        var result = Resolve(settings, s_builtInImageOnly, @"C:\w\praia.jpg");

        Assert.Equal(@"C:\w\praia.jpg", result[0].Path);
        Assert.True(result[1].IsGearboxWallpaper);
    }

    [Fact]
    public void ArquivoApagado_CaiNoGearboxEAvisa()
    {
        var settings = new WallpaperSettings { Default = new MonitorWallpaper { Path = @"C:\w\sumiu.mp4" } };

        var result = Resolve(settings, s_builtInImageOnly);

        Assert.All(result, a =>
        {
            Assert.True(a.IsGearboxWallpaper);
            Assert.Equal(WallpaperProblem.FileNotFound, a.Problem);
        });
    }

    [Fact]
    public void FormatoNaoSuportado_CaiNoOriginalSemGearboxEAvisa()
    {
        var settings = new WallpaperSettings
        {
            UseGearboxWallpaper = false,
            Default = new MonitorWallpaper { Path = @"C:\w\anim.gif" },
        };

        var result = Resolve(settings, s_builtInImageOnly, @"C:\w\anim.gif");

        Assert.All(result, a =>
        {
            Assert.True(a.KeepsOriginal);
            Assert.Equal(WallpaperProblem.UnsupportedFormat, a.Problem);
        });
    }

    [Fact]
    public void VideoQueFalhou_CaiNoGearboxEAvisa()
    {
        var settings = new WallpaperSettings { Default = new MonitorWallpaper { Path = @"C:\w\loop.webm" } };
        var failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\w\LOOP.webm" };

        var result = WallpaperResolver.Resolve(settings, [Monitor1], s_builtInImageOnly, _ => true, failed);

        var assignment = Assert.Single(result);
        Assert.True(assignment.IsGearboxWallpaper);
        Assert.Equal(WallpaperProblem.PlaybackFailed, assignment.Problem);
    }

    [Fact]
    public void VideoEmbutidoQueFalhou_CaiNaImagemEmbutida()
    {
        var failed = new HashSet<string> { s_builtInWithVideo.VideoPath! };

        var result = WallpaperResolver.Resolve(new WallpaperSettings(), [Monitor1], s_builtInWithVideo, _ => true, failed);

        Assert.Equal((s_builtInWithVideo.ImagePath, MediaKind.Image), (result[0].Path, result[0].Kind));
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

        var result = WallpaperResolver.Resolve(settings, [@"\\?\DISPLAY#NOVO#3"], s_builtInImageOnly, _ => true);

        Assert.Equal(@"C:\w\geral.jpg", Assert.Single(result).Path);
    }
}
