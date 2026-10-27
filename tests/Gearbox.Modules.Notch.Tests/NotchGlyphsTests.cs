using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests;

public sealed class NotchGlyphsTests
{
    [Theory]
    [InlineData(0.5f, true)]
    [InlineData(0f, false)]
    public void Volume_MudoOuZerado_MostraMudo(float level, bool muted)
    {
        Assert.Equal(NotchGlyphs.Mute, NotchGlyphs.Volume(level, muted));
    }

    [Theory]
    [InlineData(0.1f, NotchGlyphs.VolumeLow)]
    [InlineData(0.5f, NotchGlyphs.VolumeMedium)]
    [InlineData(1f, NotchGlyphs.VolumeHigh)]
    public void Volume_PorNivel(float level, string expected)
    {
        Assert.Equal(expected, NotchGlyphs.Volume(level, muted: false));
    }

    [Theory]
    [InlineData(0, NotchGlyphs.BatteryEmpty)]
    [InlineData(9, NotchGlyphs.BatteryEmpty)]
    [InlineData(10, NotchGlyphs.BatteryLow)]
    [InlineData(52, NotchGlyphs.BatteryMedium)]
    [InlineData(80, NotchGlyphs.BatteryHigh)]
    [InlineData(90, NotchGlyphs.BatteryFull)]
    [InlineData(100, NotchGlyphs.BatteryFull)]
    public void Battery_Descarregando_MostraONivel(int percent, string expected)
    {
        Assert.Equal(expected, NotchGlyphs.Battery(percent, charging: false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(100)]
    public void Battery_Carregando_MostraORaioEmQualquerNivel(int percent)
    {
        Assert.Equal(NotchGlyphs.BatteryCharging, NotchGlyphs.Battery(percent, charging: true));
    }

    [Theory]
    [InlineData(-10, NotchGlyphs.BatteryEmpty)]
    [InlineData(250, NotchGlyphs.BatteryFull)]
    public void Battery_ForaDaFaixa_ELimitada(int percent, string expected)
    {
        Assert.Equal(expected, NotchGlyphs.Battery(percent, charging: false));
    }

    [Theory]
    [InlineData(RadioPower.On, NotchGlyphs.WiFiOn, NotchGlyphs.BluetoothOn)]
    [InlineData(RadioPower.Off, NotchGlyphs.WiFiOff, NotchGlyphs.BluetoothOff)]
    [InlineData(RadioPower.Unavailable, NotchGlyphs.WiFiOff, NotchGlyphs.BluetoothOff)]
    public void Radios_LigadoOuCortado(RadioPower power, string wifi, string bluetooth)
    {
        Assert.Equal(wifi, NotchGlyphs.WiFi(power));
        Assert.Equal(bluetooth, NotchGlyphs.Bluetooth(power));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.333f, 33)]
    [InlineData(1f, 100)]
    [InlineData(1.5f, 100)]
    [InlineData(-1f, 0)]
    public void Percent_Arredonda(float level, int expected)
    {
        Assert.Equal(expected, NotchGlyphs.Percent(level));
    }

    [Theory]
    [InlineData(50, false, BatteryStatusKind.OnBattery)]
    [InlineData(100, false, BatteryStatusKind.OnBattery)]
    [InlineData(50, true, BatteryStatusKind.Charging)]
    [InlineData(99, true, BatteryStatusKind.Charged)]
    public void BatteryInfo_Status(int percent, bool pluggedIn, BatteryStatusKind expected)
    {
        Assert.Equal(expected, new BatteryInfo(percent, pluggedIn).Status);
    }
}
