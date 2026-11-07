using Gearbox.Interop.Geometry;

namespace Gearbox.Modules.Notch.Logic;

public readonly record struct NotchRegionShape(
    PixelRect Bounds,
    PixelRect Relative,
    PixelCircle LeftCutout,
    PixelCircle RightCutout,
    PixelRect LeftGap,
    PixelRect RightGap);

public static class NotchRegion
{
    public static NotchRegionShape Compute(PixelRect window, PixelRect monitor, double dpiScale, DipSize body, double earRadius)
    {
        var ear = Math.Max(0, earRadius);
        var shape = NotchLayout.Place(monitor, dpiScale, NotchLayout.WindowSize(body, ear));
        var bounds = new PixelRect(
            Math.Max(window.Left, shape.Left - 1),
            shape.Top,
            Math.Min(window.Right, shape.Right + 1),
            Math.Min(window.Bottom, shape.Bottom + 1));
        var relative = new PixelRect(
            bounds.Left - window.Left,
            bounds.Top - window.Top,
            bounds.Right - window.Left,
            bounds.Bottom - window.Top);

        var scale = dpiScale > 0 ? dpiScale : 1.0;
        var radius = (int)Math.Round(ear * scale);
        if (radius <= 0)
        {
            return new NotchRegionShape(bounds, relative, default, default, default, default);
        }

        var top = shape.Top - window.Top + radius;
        var left = new PixelCircle(shape.Left - window.Left, top, radius);
        var right = new PixelCircle(shape.Right - window.Left, top, radius);
        var leftGap = new PixelRect(relative.Left, top, shape.Left - window.Left + radius - 1, relative.Bottom);
        var rightGap = new PixelRect(shape.Right - window.Left - radius + 1, top, relative.Right, relative.Bottom);
        return new NotchRegionShape(bounds, relative, left, right, leftGap, rightGap);
    }
}
