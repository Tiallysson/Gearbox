using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class ClipboardThumbnailTests
{
    [Theory]
    [InlineData(100u, 80u, 1.0)]
    [InlineData(1920u, 1080u, 128.0 / 1080)]
    [InlineData(20000u, 1000u, 256.0 / 20000)]
    [InlineData(1000u, 20000u, 128.0 / 20000)]
    public void Scale_LimitaPelaAlturaEPelaLargura(uint width, uint height, double expected)
    {
        Assert.Equal(expected, ClipboardThumbnail.Scale(width, height), 9);
    }

    [Fact]
    public void Scale_DimensaoZero_NaoDivide()
    {
        Assert.Equal(1.0, ClipboardThumbnail.Scale(0, 0));
    }

    [Fact]
    public void Scale_PanoramaGigante_FicaDentroDoLimite()
    {
        var scale = ClipboardThumbnail.Scale(60000, 4000);

        Assert.True(ClipboardThumbnail.Scaled(60000, scale) <= ClipboardThumbnail.MaxWidth);
        Assert.True(ClipboardThumbnail.Scaled(4000, scale) <= ClipboardThumbnail.MaxHeight);
    }

    [Theory]
    [InlineData(1000u, 0.5, 500u)]
    [InlineData(3u, 0.5, 2u)]
    [InlineData(10u, 0.0001, 1u)]
    public void Scaled_ArredondaENuncaFicaAbaixoDeUm(uint pixels, double scale, uint expected)
    {
        Assert.Equal(expected, ClipboardThumbnail.Scaled(pixels, scale));
    }
}
