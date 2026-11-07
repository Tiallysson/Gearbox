using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Xaml.Media;

namespace Gearbox.Modules.Notch.UI;

public enum ColorSwatchKind
{
    Preset,
    Accent,
    Custom,
}

public sealed class ColorSwatch(ColorSwatchKind kind, TintColor color, string name)
{
    public ColorSwatchKind Kind { get; } = kind;

    public TintColor Color { get; } = color;

    public string Name { get; } = name;

    public SolidColorBrush Brush { get; } = new(Windows.UI.Color.FromArgb(255, color.R, color.G, color.B));

    public string Glyph => Kind switch
    {
        ColorSwatchKind.Accent => "",
        ColorSwatchKind.Custom => "",
        _ => string.Empty,
    };
}
