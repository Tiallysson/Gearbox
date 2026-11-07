using Gearbox.Interop.Geometry;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class NotchRegionTests
{
    private static readonly PixelRect s_monitor = new(0, 0, 1920, 1080);
    private static readonly PixelRect s_window = new(760, 0, 1160, 300);

    [Fact]
    public void Compute_SemOrelhas_NaoRecorta()
    {
        var region = NotchRegion.Compute(s_window, s_monitor, 1.0, new DipSize(180, 30), 0);

        Assert.Equal(new PixelRect(869, 0, 1051, 31), region.Bounds);
        Assert.Equal(new PixelRect(109, 0, 291, 31), region.Relative);
        Assert.True(region.LeftCutout.IsEmpty);
        Assert.True(region.RightCutout.IsEmpty);
        Assert.True(region.LeftGap.IsEmpty);
        Assert.True(region.RightGap.IsEmpty);
    }

    [Fact]
    public void Compute_ComOrelhas_RecortaUmCirculoEmCadaCantoDeCima()
    {
        var region = NotchRegion.Compute(s_window, s_monitor, 1.0, new DipSize(320, 40), 10);

        Assert.Equal(new PixelRect(789, 0, 1131, 41), region.Bounds);
        Assert.Equal(new PixelCircle(30, 10, 10), region.LeftCutout);
        Assert.Equal(new PixelCircle(370, 10, 10), region.RightCutout);
    }

    [Fact]
    public void Compute_ComOrelhas_TiraAFaixaVaziaAbaixoDeCadaOrelhaMantendoAMargemDoCorpo()
    {
        var region = NotchRegion.Compute(s_window, s_monitor, 1.0, new DipSize(320, 40), 10);

        Assert.Equal(new PixelRect(29, 10, 39, 41), region.LeftGap);
        Assert.Equal(new PixelRect(361, 10, 371, 41), region.RightGap);
    }

    [Fact]
    public void Compute_AplicaEscalaDeDpiNoRaio()
    {
        var region = NotchRegion.Compute(new PixelRect(0, 0, 1920, 600), s_monitor, 1.5, new DipSize(320, 40), 10);

        Assert.Equal(15, region.LeftCutout.Radius);
        Assert.Equal(15, region.LeftCutout.CenterY);
    }

    [Fact]
    public void Compute_RaioNegativo_ViraZero()
    {
        var region = NotchRegion.Compute(s_window, s_monitor, 1.0, new DipSize(180, 30), -4);

        Assert.True(region.LeftCutout.IsEmpty);
        Assert.Equal(new PixelRect(869, 0, 1051, 31), region.Bounds);
    }
}
