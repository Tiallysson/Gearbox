using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Gearbox.Modules.Notch.UI;

public sealed partial class NotchTheme : ResourceDictionary
{
    private static readonly Dictionary<string, Color> s_darkText = new()
    {
        ["ForegroundBrush"] = Rgb(0x0A, 0x0A, 0x0A),
        ["MutedForegroundBrush"] = Rgb(0x4D, 0x4D, 0x4D),
        ["PrimaryBrush"] = Rgb(0x17, 0x17, 0x17),
        ["PrimaryHoverBrush"] = Rgb(0x38, 0x38, 0x38),
        ["PrimaryForegroundBrush"] = Rgb(0xFA, 0xFA, 0xFA),
        ["PrimaryTrackBrush"] = Color.FromArgb(0x33, 0x17, 0x17, 0x17),
        ["DestructiveBrush"] = Rgb(0xDC, 0x26, 0x26),
        ["MutedBrush"] = Color.FromArgb(0x14, 0, 0, 0),
        ["AccentBrush"] = Color.FromArgb(0x1A, 0, 0, 0),
        ["AccentPressedBrush"] = Color.FromArgb(0x26, 0, 0, 0),
        ["InputBrush"] = Color.FromArgb(0x26, 0, 0, 0),
        ["InputFillBrush"] = Color.FromArgb(0x0B, 0, 0, 0),
        ["SliderTrackFill"] = Color.FromArgb(0x26, 0, 0, 0),
        ["SliderTrackFillPointerOver"] = Color.FromArgb(0x26, 0, 0, 0),
        ["SliderTrackFillPressed"] = Color.FromArgb(0x26, 0, 0, 0),
        ["SliderThumbBackground"] = Rgb(0xFA, 0xFA, 0xFA),
        ["SliderThumbBackgroundPointerOver"] = Rgb(0xFA, 0xFA, 0xFA),
        ["SliderThumbBackgroundPressed"] = Rgb(0xFA, 0xFA, 0xFA),
    };

    private Dictionary<string, Color>? _lightText;
    private TextTone _tone = TextTone.Light;

    public NotchTheme()
    {
        InitializeComponent();
    }

    public void ApplyTone(TextTone tone)
    {
        if (tone == _tone)
        {
            return;
        }

        _lightText ??= s_darkText.Keys.ToDictionary(key => key, key => Brush(key).Color);
        var palette = tone == TextTone.Dark ? s_darkText : _lightText;
        foreach (var (key, color) in palette)
        {
            Brush(key).Color = color;
        }

        _tone = tone;
    }

    private SolidColorBrush Brush(string key) => (SolidColorBrush)this[key];

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(0xFF, r, g, b);
}
