using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests;

public sealed class NotchAppearanceTests
{
    [Theory]
    [InlineData(100, 100)]
    [InlineData(55, 55)]
    [InlineData(20, 20)]
    [InlineData(5, 20)]
    [InlineData(-30, 20)]
    [InlineData(250, 100)]
    public void ClampOpacity_LimitaAFaixa(int percent, int expected)
    {
        Assert.Equal(expected, NotchAppearance.ClampOpacity(percent));
    }

    [Theory]
    [InlineData(100, 1.0)]
    [InlineData(60, 0.6)]
    [InlineData(0, 0.2)]
    public void ToBrushOpacity_ViraFracao(int percent, double expected)
    {
        Assert.Equal(expected, NotchAppearance.ToBrushOpacity(percent), precision: 3);
    }

    [Fact]
    public void Padrao_AcrilicoComTomDe60PorCento()
    {
        var settings = new NotchSettings();

        Assert.True(settings.AcrylicBackground);
        Assert.Equal(60, settings.BackgroundOpacity);
    }
}
