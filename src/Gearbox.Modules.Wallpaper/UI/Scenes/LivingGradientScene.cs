using System.Numerics;

namespace Gearbox.Modules.Wallpaper.UI.Scenes;

internal sealed class LivingGradientScene : WallpaperScene
{
    protected override void Build(Vector2 size)
    {
        var background = Own(Compositor.CreateLinearGradientBrush());
        background.StartPoint = new Vector2(0, 0);
        background.EndPoint = new Vector2(1, 1);
        var first = Stop(0, Argb(255, 0x1A1446));
        var middle = Stop(0.55f, Argb(255, 0x3B1E7A));
        var last = Stop(1, Argb(255, 0x0E2A5C));
        background.ColorStops.Add(first);
        background.ColorStops.Add(middle);
        background.ColorStops.Add(last);
        AddSprite(size, Vector3.Zero, background);

        Animate(first, "Color", Loop(TimeSpan.FromSeconds(46), Argb(255, 0x1A1446), Argb(255, 0x0E3B4F), Argb(255, 0x2A1052), Argb(255, 0x1A1446)));
        Animate(middle, "Color", Loop(TimeSpan.FromSeconds(38), Argb(255, 0x3B1E7A), Argb(255, 0x7A2F6B), Argb(255, 0x1F4C8A), Argb(255, 0x3B1E7A)));
        Animate(last, "Color", Loop(TimeSpan.FromSeconds(52), Argb(255, 0x0E2A5C), Argb(255, 0x431452), Argb(255, 0x0B3A47), Argb(255, 0x0E2A5C)));
        Animate(background, "StartPoint", Loop(TimeSpan.FromSeconds(60), true, new Vector2(0, 0), new Vector2(0.4f, 0), new Vector2(0, 0.3f)));
        Animate(background, "EndPoint", Loop(TimeSpan.FromSeconds(60), true, new Vector2(1, 1), new Vector2(0.7f, 1), new Vector2(1, 0.6f)));

        var span = Math.Max(size.X, size.Y);
        AddBlob(size, span * 0.95f, new Vector2(0.15f, 0.10f), new Vector2(0.45f, 0.35f), 0xFF6B8B, 150, 44);
        AddBlob(size, span * 1.10f, new Vector2(0.80f, 0.70f), new Vector2(0.55f, 0.45f), 0x6A5CFF, 160, 57);
        AddBlob(size, span * 0.80f, new Vector2(0.75f, 0.05f), new Vector2(0.95f, 0.40f), 0x2BD4C4, 120, 66);
        AddBlob(size, span * 0.70f, new Vector2(0.10f, 0.90f), new Vector2(0.35f, 0.65f), 0xFFA46B, 110, 49);
    }

    private void AddBlob(Vector2 size, float diameter, Vector2 from, Vector2 to, uint color, byte alpha, int seconds)
    {
        var blobSize = new Vector2(diameter);
        var start = new Vector3((from.X * size.X) - (diameter / 2), (from.Y * size.Y) - (diameter / 2), 0);
        var end = new Vector3((to.X * size.X) - (diameter / 2), (to.Y * size.Y) - (diameter / 2), 0);
        var blob = AddSprite(blobSize, start, Glow(Argb(alpha, color), Argb((byte)(alpha / 2), color), 0.4));
        blob.CenterPoint = new Vector3(diameter / 2, diameter / 2, 0);
        Animate(blob, "Offset", Loop(TimeSpan.FromSeconds(seconds), true, start, Vector3.Lerp(start, end, 0.6f) + new Vector3(diameter * 0.08f, -diameter * 0.05f, 0), end));
        Animate(blob, "Scale", Loop(TimeSpan.FromSeconds(seconds * 0.6), true, new Vector3(1), new Vector3(1.18f, 0.92f, 1), new Vector3(0.94f, 1.1f, 1)));
    }
}
