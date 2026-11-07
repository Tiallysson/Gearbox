using Gearbox.Modules.Wallpaper.Logic.Gallery;

namespace Gearbox.Modules.Wallpaper.Tests.Logic.Gallery;

public sealed class AuroraRibbonTextureTests
{
    [Fact]
    public void Render_TemOTamanhoDaTexturaEEDeterministico()
    {
        var first = AuroraRibbonTexture.Render(1);

        Assert.Equal(AuroraRibbonTexture.Width * AuroraRibbonTexture.Height * 4, first.Length);
        Assert.Equal(first, AuroraRibbonTexture.Render(1));
        Assert.NotEqual(first, AuroraRibbonTexture.Render(4));
    }

    [Fact]
    public void Render_CorPreMultiplicadaNuncaPassaDoAlfa()
    {
        var pixels = AuroraRibbonTexture.Render(7);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            Assert.True(pixels[i] <= pixels[i + 3] && pixels[i + 1] <= pixels[i + 3] && pixels[i + 2] <= pixels[i + 3]);
        }
    }

    [Fact]
    public void Render_BordasLateraisTransparentesECentroVisivel()
    {
        var pixels = AuroraRibbonTexture.Render(1);
        var leftEdgeAlpha = Enumerable.Range(0, AuroraRibbonTexture.Height).Max(y => pixels[(y * AuroraRibbonTexture.Width * 4) + 3]);
        var centerAlpha = Enumerable.Range(0, AuroraRibbonTexture.Height).Max(y => pixels[(((y * AuroraRibbonTexture.Width) + (AuroraRibbonTexture.Width / 2)) * 4) + 3]);

        Assert.Equal(0, leftEdgeAlpha);
        Assert.True(centerAlpha > 100);
    }
}
