namespace Gearbox.Modules.Notch.Logic;

internal static class ClipboardThumbnail
{
    public const uint MaxHeight = 128;
    public const uint MaxWidth = 256;

    public static double Scale(uint orientedWidth, uint orientedHeight) =>
        Math.Min(1.0, Math.Min((double)MaxWidth / Math.Max(1u, orientedWidth), (double)MaxHeight / Math.Max(1u, orientedHeight)));

    public static uint Scaled(uint pixels, double scale) => (uint)Math.Max(1, Math.Round(pixels * scale));
}
