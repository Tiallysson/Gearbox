using System.Globalization;

namespace Gearbox.Modules.Notch.Logic;

public enum BackgroundStyle
{
    Opaque,
    Subtle,
    Translucent,
    Clear,
    Glass,
}

public enum NotchRoundness
{
    Less,
    Default,
    More,
}

public readonly record struct TintColor(byte R, byte G, byte B)
{
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public static bool TryParse(string? text, out TintColor color)
    {
        color = default;
        var hex = text?.Trim().TrimStart('#');
        if (hex is not { Length: 6 } || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        color = new TintColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }
}

public readonly record struct BackgroundLook(bool Blur, double Opacity);

public readonly record struct PaletteColor(string Key, TintColor Color);

public static class NotchAppearance
{
    public const double MaxRoundnessFactor = 1.5;

    public const double MaxReadableLuma = 0.3;

    public static readonly TintColor Black = new(0x0A, 0x0A, 0x0A);

    public static readonly PaletteColor[] Palette =
    [
        new("Black", Black),
        new("Graphite", new TintColor(0x1F, 0x21, 0x26)),
        new("Midnight", new TintColor(0x0B, 0x14, 0x2B)),
        new("Purple", new TintColor(0x2A, 0x18, 0x40)),
        new("Green", new TintColor(0x0F, 0x2A, 0x1F)),
        new("Wine", new TintColor(0x3A, 0x0F, 0x1A)),
    ];

    public static BackgroundLook Look(BackgroundStyle style) => style switch
    {
        BackgroundStyle.Opaque => new BackgroundLook(false, 1.0),
        BackgroundStyle.Subtle => new BackgroundLook(false, 0.85),
        BackgroundStyle.Clear => new BackgroundLook(false, 0.35),
        BackgroundStyle.Glass => new BackgroundLook(true, 0.35),
        _ => new BackgroundLook(false, 0.6),
    };

    public static BackgroundStyle StyleFromLegacy(bool acrylic, int opacityPercent)
    {
        if (acrylic)
        {
            return BackgroundStyle.Glass;
        }

        return opacityPercent switch
        {
            >= 93 => BackgroundStyle.Opaque,
            >= 73 => BackgroundStyle.Subtle,
            >= 48 => BackgroundStyle.Translucent,
            _ => BackgroundStyle.Clear,
        };
    }

    public static TintColor Readable(TintColor color)
    {
        var luma = ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) / 255.0;
        if (luma <= MaxReadableLuma)
        {
            return color;
        }

        var scale = MaxReadableLuma / luma;
        return new TintColor(
            (byte)Math.Round(color.R * scale),
            (byte)Math.Round(color.G * scale),
            (byte)Math.Round(color.B * scale));
    }

    public static TintColor ParseOrDefault(string? hex) => TintColor.TryParse(hex, out var color) ? color : Black;

    public static double RoundnessFactor(NotchRoundness roundness) => roundness switch
    {
        NotchRoundness.Less => 0.5,
        NotchRoundness.More => MaxRoundnessFactor,
        _ => 1.0,
    };
}
