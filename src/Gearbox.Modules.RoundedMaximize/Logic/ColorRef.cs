using System.Globalization;

namespace Gearbox.Modules.RoundedMaximize.Logic;

/// <summary>Conversões de cor para o formato COLORREF do Win32 (0x00BBGGRR).</summary>
public static class ColorRef
{
    public static uint FromRgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));

    /// <summary>Lê <c>#RRGGBB</c> ou <c>#AARRGGBB</c> (o alfa é ignorado).</summary>
    public static bool TryParseHex(string? hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        var span = (hex ?? string.Empty).AsSpan().Trim().TrimStart('#');
        if (span.Length == 8)
        {
            span = span[2..];
        }

        if (span.Length != 6 || !uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        r = (byte)(value >> 16);
        g = (byte)(value >> 8);
        b = (byte)value;
        return true;
    }

    public static string ToHex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";
}
