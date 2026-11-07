using System.Numerics;

namespace Gearbox.Modules.Wallpaper.UI.Scenes;

internal sealed class ParticlesScene : WallpaperScene
{
    private static readonly (int Count, float MinSize, float MaxSize, float MinOpacity, float MaxOpacity, int MinSeconds, int MaxSeconds, float Travel)[] s_layers =
    [
        (46, 2, 4, 0.20f, 0.45f, 40, 70, 0.10f),
        (26, 5, 10, 0.30f, 0.60f, 28, 46, 0.18f),
        (11, 18, 42, 0.12f, 0.30f, 20, 34, 0.30f),
    ];

    private static readonly uint[] s_palette = [0xE8F1FF, 0x9FE3FF, 0xC9B8FF, 0xFFD9A8];

    protected override void Build(Vector2 size)
    {
        var background = Own(Compositor.CreateRadialGradientBrush());
        background.EllipseCenter = new Vector2(0.5f, 0.38f);
        background.EllipseRadius = new Vector2(0.85f, 0.95f);
        background.ColorStops.Add(Stop(0, Argb(255, 0x15203A)));
        background.ColorStops.Add(Stop(0.55f, Argb(255, 0x0A1022)));
        background.ColorStops.Add(Stop(1, Argb(255, 0x04050A)));
        AddSprite(size, Vector3.Zero, background);

        var haze = AddSprite(new Vector2(size.X * 1.2f, size.Y * 0.9f), new Vector3(-size.X * 0.1f, size.Y * 0.35f, 0), Glow(Argb(40, 0x4A6CFF), Argb(18, 0x2B3D99), 0.4));
        Animate(haze, "Opacity", Loop(TimeSpan.FromSeconds(24), true, 0.6f, 1f));

        var brushes = s_palette.Select(c => Glow(Argb(255, c), Argb(90, c), 0.35)).ToArray();
        var random = new Random(9049);
        var scale = Math.Max(1f, Math.Min(size.X, size.Y) / 1080f);
        foreach (var layer in s_layers)
        {
            for (var i = 0; i < layer.Count; i++)
            {
                var diameter = Lerp(layer.MinSize, layer.MaxSize, random.NextDouble()) * scale;
                var start = new Vector3((float)random.NextDouble() * size.X, (float)random.NextDouble() * size.Y, 0);
                var brush = brushes[random.NextDouble() < 0.12 ? 3 : random.Next(0, 3)];
                var particle = AddSprite(new Vector2(diameter), start, brush);
                var opacity = Lerp(layer.MinOpacity, layer.MaxOpacity, random.NextDouble());
                particle.Opacity = opacity;

                var rise = size.Y * layer.Travel;
                var sway = size.X * 0.025f * (float)(0.5 + random.NextDouble());
                var seconds = Lerp(layer.MinSeconds, layer.MaxSeconds, random.NextDouble());
                Animate(particle, "Offset", Loop(
                    TimeSpan.FromSeconds(seconds),
                    true,
                    start,
                    start + new Vector3(sway, -rise * 0.35f, 0),
                    start + new Vector3(-sway * 0.4f, -rise * 0.7f, 0),
                    start + new Vector3(sway * 0.6f, -rise, 0)));
                Animate(particle, "Opacity", Loop(TimeSpan.FromSeconds(seconds * 0.27), true, opacity, opacity * 0.45f, Math.Min(1, opacity * 1.35f)));
            }
        }
    }

    private static float Lerp(double from, double to, double t) => (float)(from + ((to - from) * t));
}
