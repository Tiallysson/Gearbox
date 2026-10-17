namespace Darwin.Modules.RoundedMaximize;

/// <summary>Gravado em <c>%LocalAppData%\Darwin\settings\RoundedMaximize.json</c>.</summary>
public sealed class RoundedMaximizeSettings
{
    public const int DefaultMargin = 8;
    public const string DefaultBorderColor = "#0078D4";

    /// <summary>Distância (px) entre a janela e as bordas da área de trabalho. 0–64.</summary>
    public int Margin { get; set; } = DefaultMargin;

    /// <summary>Cor da borda em <c>#RRGGBB</c>, usada quando <see cref="UseAccentColor"/> é falso.</summary>
    public string BorderColor { get; set; } = DefaultBorderColor;

    public bool UseAccentColor { get; set; } = true;

    /// <summary>Nomes de processo (sem <c>.exe</c>) que nunca são afetados.</summary>
    public List<string> ExcludedProcesses { get; set; } = ["explorer"];
}
