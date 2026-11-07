using System.Numerics;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using Windows.Foundation;
using Windows.UI;

namespace Gearbox.Modules.Wallpaper.UI.Scenes;

internal sealed class FluidWavesScene : WallpaperScene
{
    private const int Samples = 180;

    private static readonly (double Base, double Amplitude, double Harmonic, double Phase, uint Top, uint Bottom, byte Alpha, int Seconds, int BobSeconds)[] s_waves =
    [
        (0.52, 0.045, 0.35, 0.0, 0x2C5DFF, 0x0B1E5C, 110, 64, 19),
        (0.60, 0.050, 0.45, 1.7, 0x2BB9FF, 0x0A2A66, 120, 50, 23),
        (0.69, 0.040, 0.55, 3.1, 0x6A7BFF, 0x101A4F, 150, 41, 17),
        (0.78, 0.035, 0.40, 4.4, 0x29E2D2, 0x062A3A, 170, 33, 13),
        (0.87, 0.028, 0.60, 5.6, 0x0E1F45, 0x040915, 235, 27, 11),
    ];

    protected override void Build(Vector2 size)
    {
        AddSprite(size, Vector3.Zero, Vertical((0, Argb(255, 0x050B1C)), (0.55f, Argb(255, 0x0A1A3A)), (1, Argb(255, 0x081227))));
        var glow = AddSprite(new Vector2(size.X * 1.1f, size.Y * 0.8f), new Vector3(-size.X * 0.05f, size.Y * 0.2f, 0), Glow(Argb(60, 0x3D7BFF), Argb(20, 0x2B3DCC), 0.4));
        Animate(glow, "Opacity", Loop(TimeSpan.FromSeconds(20), true, 0.6f, 1f));

        var canvas = new Microsoft.UI.Xaml.Controls.Canvas();
        Overlay.Children.Add(canvas);
        foreach (var wave in s_waves)
        {
            var path = Wave(size, wave.Base, wave.Amplitude, wave.Harmonic, wave.Phase, wave.Top, wave.Bottom, wave.Alpha);
            var bob = new Microsoft.UI.Xaml.Controls.Canvas { Children = { path } };
            canvas.Children.Add(bob);
            ElementCompositionPreview.SetIsTranslationEnabled(path, true);
            ElementCompositionPreview.SetIsTranslationEnabled(bob, true);

            var travel = Own(Compositor.CreateVector3KeyFrameAnimation());
            travel.InsertKeyFrame(0, Vector3.Zero);
            travel.InsertKeyFrame(1, new Vector3(-size.X, 0, 0), Linear());
            travel.Duration = TimeSpan.FromSeconds(wave.Seconds);
            travel.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever;
            Animate(ElementCompositionPreview.GetElementVisual(path), "Translation", travel);

            var lift = (float)(size.Y * wave.Amplitude);
            Animate(ElementCompositionPreview.GetElementVisual(bob), "Translation", Loop(TimeSpan.FromSeconds(wave.BobSeconds), true, Vector3.Zero, new Vector3(0, -lift * 0.35f, 0), new Vector3(0, lift * 0.2f, 0)));
        }
    }

    private static Path Wave(Vector2 size, double baseLine, double amplitude, double harmonic, double phase, uint top, uint bottom, byte alpha)
    {
        var width = size.X;
        var floor = size.Y * 1.15;
        var figure = new PathFigure { StartPoint = new Point(0, floor), IsClosed = true, IsFilled = true };
        for (var i = 0; i <= Samples * 2; i++)
        {
            var x = width * 2 * i / (Samples * 2);
            var t = 2 * Math.PI * x / width;
            var y = baseLine + (amplitude * Math.Sin(t + phase)) + (amplitude * harmonic * Math.Sin((2 * t) + (phase * 1.7)));
            figure.Segments.Add(new LineSegment { Point = new Point(x, y * size.Y) });
        }

        figure.Segments.Add(new LineSegment { Point = new Point(width * 2, floor) });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        var fill = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
        fill.GradientStops.Add(new GradientStop { Offset = 0, Color = Color.FromArgb(alpha, (byte)(top >> 16), (byte)(top >> 8), (byte)top) });
        fill.GradientStops.Add(new GradientStop { Offset = 1, Color = Color.FromArgb((byte)Math.Min(255, alpha + 40), (byte)(bottom >> 16), (byte)(bottom >> 8), (byte)bottom) });
        return new Path
        {
            Data = geometry,
            Fill = fill,
            Stroke = new SolidColorBrush(Color.FromArgb(38, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = Math.Max(1, size.Y / 900),
            Width = width * 2,
            Height = floor,
        };
    }
}
