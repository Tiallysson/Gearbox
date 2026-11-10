using Gearbox.Interop.Geometry;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class TextContrastTests
{
    private static readonly TintColor White = new(255, 255, 255);

    [Fact]
    public void Vidro_PretoSobreJanelaBranca_TextoEscuro()
    {
        var glass = NotchAppearance.Look(BackgroundStyle.Glass);
        var background = TextContrast.Blend(NotchAppearance.Black, glass.Opacity, White);

        Assert.Equal(TextTone.Dark, TextContrast.Choose(TextContrast.Luminance(background), TextTone.Light));
    }

    [Fact]
    public void Opaco_SobreJanelaBranca_TextoContinuaClaro()
    {
        var opaque = NotchAppearance.Look(BackgroundStyle.Opaque);
        var background = TextContrast.Blend(NotchAppearance.Black, opaque.Opacity, White);

        Assert.Equal(TextTone.Light, TextContrast.Choose(TextContrast.Luminance(background), TextTone.Light));
    }

    [Fact]
    public void Vidro_SobreJanelaEscura_TextoClaro()
    {
        var glass = NotchAppearance.Look(BackgroundStyle.Glass);
        var background = TextContrast.Blend(NotchAppearance.Black, glass.Opacity, new TintColor(0x20, 0x20, 0x20));

        Assert.Equal(TextTone.Light, TextContrast.Choose(TextContrast.Luminance(background), TextTone.Dark));
    }

    [Theory]
    [InlineData(0.18, TextTone.Light, TextTone.Light)]
    [InlineData(0.18, TextTone.Dark, TextTone.Dark)]
    [InlineData(0.30, TextTone.Light, TextTone.Dark)]
    [InlineData(0.05, TextTone.Dark, TextTone.Light)]
    public void Choose_NaFaixaIntermediariaMantemOTomAtual(double luminance, TextTone current, TextTone expected)
    {
        Assert.Equal(expected, TextContrast.Choose(luminance, current));
    }

    [Fact]
    public void Luminance_PretoZeroBrancoUm()
    {
        Assert.Equal(0, TextContrast.Luminance(new TintColor(0, 0, 0)), 6);
        Assert.Equal(1, TextContrast.Luminance(White), 6);
    }

    [Fact]
    public void Average_MediaDeTodasAsCapturasEmBgra()
    {
        byte[] black = [0, 0, 0, 255];
        byte[] blueAndRed = [255, 0, 0, 255, 0, 0, 255, 255];

        var average = TextContrast.Average([black, blueAndRed]);

        Assert.Equal(new TintColor(85, 0, 85), average);
    }

    [Fact]
    public void Average_SemPixels_Nulo()
    {
        Assert.Null(TextContrast.Average([]));
    }

    [Fact]
    public void SampleAreas_FaixasAosLadosEAbaixoForaDaIlha()
    {
        var monitor = PixelRect.FromSize(0, 0, 1920, 1080);
        var island = new PixelRect(800, 0, 1120, 40);

        var areas = TextContrast.SampleAreas(island, monitor, 1.0);

        Assert.Equal(
            [new PixelRect(780, 0, 796, 40), new PixelRect(1124, 0, 1140, 40), new PixelRect(800, 44, 1120, 60)],
            areas);
    }

    [Fact]
    public void SampleAreas_RecortaNoMonitorEDescartaFaixasVazias()
    {
        var monitor = PixelRect.FromSize(0, 0, 1000, 50);
        var island = new PixelRect(2, 0, 998, 48);

        var areas = TextContrast.SampleAreas(island, monitor, 1.0);

        Assert.Empty(areas);
    }
}
