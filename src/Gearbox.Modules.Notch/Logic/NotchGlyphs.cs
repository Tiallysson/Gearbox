namespace Gearbox.Modules.Notch.Logic;

/// <summary>
/// Ícones da ilha: codepoints da fonte Phosphor Icons (regular), em <c>Assets\Fonts\Phosphor.ttf</c>. Os ícones fixos
/// ficam direto no XAML (<c>NotchView.xaml</c>); aqui só os que mudam com o estado.
/// </summary>
public static class NotchGlyphs
{
    public const string Play = ""; // play
    public const string Pause = ""; // pause
    public const string MusicNote = ""; // music-note

    public const string Mute = ""; // speaker-x
    public const string VolumeLow = ""; // speaker-none
    public const string VolumeMedium = ""; // speaker-low
    public const string VolumeHigh = ""; // speaker-high

    public const string WiFiOn = ""; // wifi-high
    public const string WiFiOff = ""; // wifi-slash
    public const string BluetoothOn = ""; // bluetooth
    public const string BluetoothOff = ""; // bluetooth-slash
    public const string Ethernet = ""; // network (o Phosphor não tem "ethernet")

    public const string HardDrive = ""; // hard-drive
    public const string Usb = ""; // usb

    public const string BatteryEmpty = ""; // battery-empty
    public const string BatteryLow = ""; // battery-low
    public const string BatteryMedium = ""; // battery-medium
    public const string BatteryHigh = ""; // battery-high
    public const string BatteryFull = ""; // battery-full
    public const string BatteryCharging = ""; // battery-charging

    /// <param name="level">De 0 a 1.</param>
    public static string Volume(float level, bool muted)
    {
        if (muted || level <= 0.005f)
        {
            return Mute;
        }

        return level switch
        {
            < 0.34f => VolumeLow,
            < 0.67f => VolumeMedium,
            _ => VolumeHigh,
        };
    }

    /// <summary>
    /// O Phosphor tem cinco níveis de bateria e um só ícone de carregando (sem nível): na tomada, o raio; fora dela,
    /// o nível. A porcentagem aparece ao lado em todo lugar que mostra o ícone.
    /// </summary>
    /// <param name="percent">De 0 a 100.</param>
    public static string Battery(int percent, bool charging)
    {
        if (charging)
        {
            return BatteryCharging;
        }

        return Math.Clamp(percent, 0, 100) switch
        {
            < 10 => BatteryEmpty,
            < 35 => BatteryLow,
            < 65 => BatteryMedium,
            < 90 => BatteryHigh,
            _ => BatteryFull,
        };
    }

    public static string WiFi(RadioPower power) => power == RadioPower.On ? WiFiOn : WiFiOff;

    public static string Bluetooth(RadioPower power) => power == RadioPower.On ? BluetoothOn : BluetoothOff;

    /// <summary>Volume em porcentagem inteira (0–100).</summary>
    public static int Percent(float level) => (int)Math.Round(Math.Clamp(level, 0f, 1f) * 100);
}
