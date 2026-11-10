using Gearbox.Interop.Geometry;

namespace Gearbox.Modules.Notch.Logic;

public enum TextTone
{
    Light,
    Dark,
}

public static class TextContrast
{
    public const double DarkTextAbove = 0.24;

    public const double LightTextBelow = 0.13;

    private const double SampleGapDip = 4;

    private const double SampleDepthDip = 16;

    public static bool ShowsWhatIsBehind(BackgroundLook look, bool windowIsTransparent) =>
        windowIsTransparent && (look.Blur || look.Opacity < 1);

    public static PixelRect[] SampleAreas(PixelRect island, PixelRect monitor, double dpiScale)
    {
        var gap = (int)Math.Round(SampleGapDip * dpiScale);
        var depth = Math.Max(1, (int)Math.Round(SampleDepthDip * dpiScale));
        PixelRect[] candidates =
        [
            new(island.Left - gap - depth, island.Top, island.Left - gap, island.Bottom),
            new(island.Right + gap, island.Top, island.Right + gap + depth, island.Bottom),
            new(island.Left, island.Bottom + gap, island.Right, island.Bottom + gap + depth),
        ];

        return [.. candidates.Select(area => Clip(area, monitor)).Where(area => !area.IsEmpty)];
    }

    public static TintColor? Average(IEnumerable<byte[]> bgraCaptures)
    {
        long blue = 0, green = 0, red = 0, pixels = 0;
        foreach (var capture in bgraCaptures)
        {
            for (var i = 0; i + 3 < capture.Length; i += 4)
            {
                blue += capture[i];
                green += capture[i + 1];
                red += capture[i + 2];
                pixels++;
            }
        }

        if (pixels == 0)
        {
            return null;
        }

        return new TintColor((byte)(red / pixels), (byte)(green / pixels), (byte)(blue / pixels));
    }

    public static TintColor Blend(TintColor tint, double opacity, TintColor behind)
    {
        var alpha = Math.Clamp(opacity, 0, 1);
        return new TintColor(Mix(tint.R, behind.R, alpha), Mix(tint.G, behind.G, alpha), Mix(tint.B, behind.B, alpha));
    }

    public static double Luminance(TintColor color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    public static TextTone Choose(double backgroundLuminance, TextTone current) => current switch
    {
        TextTone.Light when backgroundLuminance > DarkTextAbove => TextTone.Dark,
        TextTone.Dark when backgroundLuminance < LightTextBelow => TextTone.Light,
        _ => current,
    };

    private static PixelRect Clip(PixelRect area, PixelRect bounds) => new(
        Math.Max(area.Left, bounds.Left),
        Math.Max(area.Top, bounds.Top),
        Math.Min(area.Right, bounds.Right),
        Math.Min(area.Bottom, bounds.Bottom));

    private static byte Mix(byte front, byte back, double alpha) =>
        (byte)Math.Round((front * alpha) + (back * (1 - alpha)));

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
