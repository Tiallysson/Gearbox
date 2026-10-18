using Gearbox.Modules.RoundedMaximize.Logic;

namespace Gearbox.Modules.RoundedMaximize.Tests;

public sealed class ColorRefTests
{
    [Fact]
    public void FromRgb_UsaOFormato00BBGGRR()
    {
        Assert.Equal(0x00D47800u, ColorRef.FromRgb(0x00, 0x78, 0xD4));
        Assert.Equal(0x000000FFu, ColorRef.FromRgb(0xFF, 0x00, 0x00));
        Assert.Equal(0x00FF0000u, ColorRef.FromRgb(0x00, 0x00, 0xFF));
    }

    [Theory]
    [InlineData("#0078D4", 0x00, 0x78, 0xD4)]
    [InlineData("0078d4", 0x00, 0x78, 0xD4)]
    [InlineData("#FF0078D4", 0x00, 0x78, 0xD4)]
    [InlineData("  #ffffff ", 0xFF, 0xFF, 0xFF)]
    public void TryParseHex_FormatosValidos(string hex, byte r, byte g, byte b)
    {
        Assert.True(ColorRef.TryParseHex(hex, out var pr, out var pg, out var pb));
        Assert.Equal((r, g, b), (pr, pg, pb));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGHHII")]
    [InlineData("azul")]
    public void TryParseHex_FormatosInvalidos(string? hex)
    {
        Assert.False(ColorRef.TryParseHex(hex, out _, out _, out _));
    }

    [Fact]
    public void ToHex_IdaEVolta()
    {
        var hex = ColorRef.ToHex(0x12, 0xAB, 0x0F);

        Assert.Equal("#12AB0F", hex);
        Assert.True(ColorRef.TryParseHex(hex, out var r, out var g, out var b));
        Assert.Equal((0x12, 0xAB, 0x0F), (r, g, b));
    }
}
