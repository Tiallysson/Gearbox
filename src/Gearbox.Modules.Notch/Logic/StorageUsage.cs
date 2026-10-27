using System.Globalization;

namespace Gearbox.Modules.Notch.Logic;

/// <summary>Uso de um disco. <paramref name="Root"/> é a raiz (<c>C:\</c>), usada para abrir no Explorer.</summary>
public sealed record DriveUsage(string Root, string Label, long TotalBytes, long FreeBytes, bool IsRemovable)
{
    /// <summary>De 0 a 100.</summary>
    public double UsedPercent => TotalBytes <= 0
        ? 0
        : Math.Clamp((TotalBytes - FreeBytes) * 100.0 / TotalBytes, 0, 100);

    /// <summary>"C:" (sem a barra).</summary>
    public string Letter => Root.TrimEnd('\\', '/');
}

/// <summary>Tamanhos como o Explorer mostra (base 1024, rótulos KB/MB/GB/TB).</summary>
public static class ByteFormat
{
    private static readonly string[] s_units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(double bytes, IFormatProvider? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < s_units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // Uma casa decimal só abaixo de 10 ("1,8 TB", "9,5 GB"); acima, inteiro ("931 GB").
        var format = unit == 0 || value >= 10 ? "{0:0} {1}" : "{0:0.#} {1}";
        return string.Format(culture, format, value, s_units[unit]);
    }
}
