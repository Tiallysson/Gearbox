using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Gearbox.Modules.Wallpaper.Logic.Gallery;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Gearbox.Modules.Wallpaper.UI.Scenes;

internal sealed class AuroraFlowScene : WallpaperScene
{
    private const int StarCount = 110;

    private static readonly Dictionary<int, Task<byte[]>> s_textures = [];

    private static readonly (int Seed, float Top, float Height, float Width, float Drift, int DriftSeconds, int BreathSeconds, float Opacity)[] s_ribbons =
    [
        (1, -0.02f, 0.78f, 1.45f, 0.10f, 74, 21, 0.95f),
        (4, 0.06f, 0.70f, 1.30f, -0.12f, 61, 17, 0.75f),
        (7, -0.08f, 0.66f, 1.20f, 0.08f, 88, 26, 0.55f),
    ];

    protected override void Build(Vector2 size)
    {
        AddSprite(size, Vector3.Zero, Vertical((0, Argb(255, 0x02040C)), (0.5f, Argb(255, 0x061328)), (0.85f, Argb(255, 0x0B2A3A))));

        var horizon = AddSprite(new Vector2(size.X * 1.3f, size.Y * 0.6f), new Vector3(-size.X * 0.15f, size.Y * 0.45f, 0), Glow(Argb(70, 0x1FD6B0), Argb(25, 0x1A6F8F), 0.4));
        Animate(horizon, "Opacity", Loop(TimeSpan.FromSeconds(19), true, 0.55f, 1f));

        var random = new Random(4127);
        var starBrush = Glow(Argb(255, 0xF2F6FF), Argb(120, 0xC8D8FF), 0.3);
        var scale = Math.Max(1f, size.Y / 1080f);
        for (var i = 0; i < StarCount; i++)
        {
            var diameter = (float)(1.5 + (random.NextDouble() * 2.5)) * scale;
            var star = AddSprite(new Vector2(diameter), new Vector3((float)random.NextDouble() * size.X, (float)Math.Pow(random.NextDouble(), 1.4) * size.Y * 0.72f, 0), starBrush);
            var dim = (float)(0.25 + (random.NextDouble() * 0.35));
            star.Opacity = dim;
            if (i % 3 == 0)
            {
                Animate(star, "Opacity", Loop(TimeSpan.FromSeconds(3 + (random.NextDouble() * 5)), true, dim, Math.Min(1, dim + 0.55f)));
            }
        }

        foreach (var ribbon in s_ribbons)
        {
            var ribbonSize = new Vector2(size.X * ribbon.Width, size.Y * ribbon.Height);
            var start = new Vector3(-(ribbonSize.X - size.X) / 2, size.Y * ribbon.Top, 0);
            var sprite = AddSprite(ribbonSize, start, Own(Compositor.CreateColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))));
            sprite.CenterPoint = new Vector3(ribbonSize.X / 2, ribbonSize.Y, 0);
            sprite.Opacity = ribbon.Opacity;
            var drift = size.X * ribbon.Drift;
            Animate(sprite, "Offset", Loop(TimeSpan.FromSeconds(ribbon.DriftSeconds), true, start, start + new Vector3(drift, -size.Y * 0.02f, 0), start + new Vector3(-drift * 0.7f, size.Y * 0.015f, 0)));
            Animate(sprite, "Scale", Loop(TimeSpan.FromSeconds(ribbon.BreathSeconds), true, new Vector3(1, 1, 1), new Vector3(1.04f, 1.18f, 1), new Vector3(0.97f, 0.88f, 1)));
            Animate(sprite, "Opacity", Loop(TimeSpan.FromSeconds(ribbon.BreathSeconds * 0.8), true, ribbon.Opacity, ribbon.Opacity * 0.55f, ribbon.Opacity));
            _ = ApplyTextureAsync(sprite, ribbon.Seed);
        }

        Overlay.Children.Add(Mountains(size));
    }

    private async Task ApplyTextureAsync(SpriteVisual sprite, int seed)
    {
        byte[] png;
        try
        {
            png = await Texture(seed);
        }
        catch (Exception)
        {
            s_textures.Remove(seed);
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        try
        {
            var stream = Own(new InMemoryRandomAccessStream());
            await stream.WriteAsync(png.AsBuffer());
            if (IsDisposed)
            {
                return;
            }

            stream.Seek(0);
            var surface = Own(LoadedImageSurface.StartLoadFromStream(stream));
            surface.LoadCompleted += (_, _) => stream.Dispose();
            var brush = Own(Compositor.CreateSurfaceBrush(surface));
            brush.Stretch = CompositionStretch.Fill;
            sprite.Brush = brush;
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static Task<byte[]> Texture(int seed)
    {
        if (!s_textures.TryGetValue(seed, out var task))
        {
            task = Task.Run(async () =>
            {
                var pixels = AuroraRibbonTexture.Render(seed);
                return await Native.PngEncoder.EncodeAsync(pixels, AuroraRibbonTexture.Width, AuroraRibbonTexture.Height, BitmapAlphaMode.Premultiplied);
            });
            s_textures[seed] = task;
        }

        return task;
    }

    private static Path Mountains(Vector2 size)
    {
        var figure = new PathFigure { StartPoint = new Windows.Foundation.Point(0, size.Y), IsClosed = true, IsFilled = true };
        const int steps = 160;
        for (var i = 0; i <= steps; i++)
        {
            var x = size.X * i / steps;
            var nx = x / size.Y;
            var ridge = 0.80 - (0.09 * Noise.Ridge(nx * 1.8, 5, 171)) - (0.02 * Noise.Fbm(nx * 12, 3.5, 3, 191));
            figure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(x, ridge * size.Y) });
        }

        figure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(size.X, size.Y) });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x03, 0x06, 0x0B)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
    }
}
