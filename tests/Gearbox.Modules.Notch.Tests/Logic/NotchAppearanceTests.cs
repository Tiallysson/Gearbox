using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class NotchAppearanceTests
{
    [Fact]
    public void Padrao_VidroPretoComMola()
    {
        var settings = new NotchSettings();

        Assert.Equal(BackgroundStyle.Glass, settings.BackgroundStyle);
        Assert.Equal("#0A0A0A", settings.BackgroundColor);
        Assert.False(settings.UseAccentColor);
        Assert.Equal(NotchRoundness.Default, settings.Roundness);
        Assert.True(settings.SpringAnimation);
        Assert.Equal(SpringIntensity.Normal, settings.SpringIntensity);
    }

    [Fact]
    public void Look_SoOVidroDesfocaEAOpacidadeCaiDoOpacoAoMuitoTransparente()
    {
        var opaque = NotchAppearance.Look(BackgroundStyle.Opaque);
        var subtle = NotchAppearance.Look(BackgroundStyle.Subtle);
        var translucent = NotchAppearance.Look(BackgroundStyle.Translucent);
        var clear = NotchAppearance.Look(BackgroundStyle.Clear);
        var glass = NotchAppearance.Look(BackgroundStyle.Glass);

        Assert.Equal(1.0, opaque.Opacity);
        Assert.True(opaque.Opacity > subtle.Opacity);
        Assert.True(subtle.Opacity > translucent.Opacity);
        Assert.True(translucent.Opacity > clear.Opacity);
        Assert.True(glass.Blur);
        Assert.False(opaque.Blur || subtle.Blur || translucent.Blur || clear.Blur);
    }

    [Theory]
    [InlineData(true, 100, BackgroundStyle.Glass)]
    [InlineData(true, 20, BackgroundStyle.Glass)]
    [InlineData(false, 100, BackgroundStyle.Opaque)]
    [InlineData(false, 80, BackgroundStyle.Subtle)]
    [InlineData(false, 60, BackgroundStyle.Translucent)]
    [InlineData(false, 20, BackgroundStyle.Clear)]
    public void StyleFromLegacy_EscolheOEstiloMaisProximo(bool acrylic, int opacity, BackgroundStyle expected)
    {
        Assert.Equal(expected, NotchAppearance.StyleFromLegacy(acrylic, opacity));
    }

    [Theory]
    [InlineData("#0B142B", 0x0B, 0x14, 0x2B)]
    [InlineData("0b142b", 0x0B, 0x14, 0x2B)]
    [InlineData(" #FFFFFF ", 0xFF, 0xFF, 0xFF)]
    public void TryParse_AceitaHexComOuSemCerquilha(string text, byte r, byte g, byte b)
    {
        Assert.True(TintColor.TryParse(text, out var color));
        Assert.Equal(new TintColor(r, g, b), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void ParseOrDefault_TextoInvalidoViraPreto(string? text)
    {
        Assert.False(TintColor.TryParse(text, out _));
        Assert.Equal(NotchAppearance.Black, NotchAppearance.ParseOrDefault(text));
    }

    [Fact]
    public void ToHex_IdaEVolta()
    {
        var color = new TintColor(0x2A, 0x18, 0x40);

        Assert.Equal("#2A1840", color.ToHex());
        Assert.True(TintColor.TryParse(color.ToHex(), out var parsed));
        Assert.Equal(color, parsed);
    }

    [Fact]
    public void Readable_CorEscuraNaoMuda()
    {
        foreach (var preset in NotchAppearance.Palette)
        {
            Assert.Equal(preset.Color, NotchAppearance.Readable(preset.Color));
        }
    }

    [Fact]
    public void Readable_CorClaraEscureceMantendoOMatiz()
    {
        var readable = NotchAppearance.Readable(new TintColor(0xFF, 0xC0, 0x40));
        var luma = ((0.299 * readable.R) + (0.587 * readable.G) + (0.114 * readable.B)) / 255.0;

        Assert.True(luma <= NotchAppearance.MaxReadableLuma + 0.01);
        Assert.True(readable.R > readable.G && readable.G > readable.B);
    }

    [Theory]
    [InlineData(NotchRoundness.Less, 0.5)]
    [InlineData(NotchRoundness.Default, 1.0)]
    [InlineData(NotchRoundness.More, 1.5)]
    public void RoundnessFactor_EscalaOsRaios(NotchRoundness roundness, double expected)
    {
        Assert.Equal(expected, NotchAppearance.RoundnessFactor(roundness));
    }
}
