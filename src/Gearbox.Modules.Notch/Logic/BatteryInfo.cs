namespace Gearbox.Modules.Notch.Logic;

public enum BatteryStatusKind
{
    OnBattery,
    Charging,
    Charged,
}

/// <summary>Bateria do PC.</summary>
/// <param name="Percent">De 0 a 100.</param>
/// <param name="IsPluggedIn">Carregador conectado.</param>
public readonly record struct BatteryInfo(int Percent, bool IsPluggedIn)
{
    /// <summary>Carga a partir da qual, na tomada, a bateria conta como carregada.</summary>
    public const int ChargedThreshold = 99;

    public BatteryStatusKind Status => !IsPluggedIn
        ? BatteryStatusKind.OnBattery
        : Percent >= ChargedThreshold ? BatteryStatusKind.Charged : BatteryStatusKind.Charging;

    public string Glyph => NotchGlyphs.Battery(Percent, IsPluggedIn);
}
