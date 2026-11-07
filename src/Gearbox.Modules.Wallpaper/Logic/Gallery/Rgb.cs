namespace Gearbox.Modules.Wallpaper.Logic.Gallery;

internal readonly record struct Rgb(double R, double G, double B)
{
    public static Rgb Hex(uint rgb) => new(((rgb >> 16) & 0xFF) / 255.0, ((rgb >> 8) & 0xFF) / 255.0, (rgb & 0xFF) / 255.0);

    public static Rgb Mix(Rgb a, Rgb b, double t) => new(a.R + ((b.R - a.R) * t), a.G + ((b.G - a.G) * t), a.B + ((b.B - a.B) * t));

    public static Rgb operator +(Rgb a, Rgb b) => new(a.R + b.R, a.G + b.G, a.B + b.B);

    public static Rgb operator *(Rgb a, double k) => new(a.R * k, a.G * k, a.B * k);

    public static Rgb Gradient(double t, params (double At, Rgb Color)[] stops)
    {
        if (t <= stops[0].At)
        {
            return stops[0].Color;
        }

        for (var i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i].At)
            {
                var span = stops[i].At - stops[i - 1].At;
                return Mix(stops[i - 1].Color, stops[i].Color, span <= 0 ? 1 : Noise.Smooth((t - stops[i - 1].At) / span));
            }
        }

        return stops[^1].Color;
    }
}

internal static class Noise
{
    public static double Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)((x * 374761393) + (y * 668265263) + (seed * 1442695041));
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h / (double)uint.MaxValue;
        }
    }

    public static double Value(double x, double y, int seed)
    {
        var xi = (int)Math.Floor(x);
        var yi = (int)Math.Floor(y);
        var fx = x - xi;
        var fy = y - yi;
        var u = fx * fx * (3 - (2 * fx));
        var v = fy * fy * (3 - (2 * fy));
        var a = Hash(xi, yi, seed);
        var b = Hash(xi + 1, yi, seed);
        var c = Hash(xi, yi + 1, seed);
        var d = Hash(xi + 1, yi + 1, seed);
        return a + ((b - a) * u) + ((c - a) * v) + ((a - b - c + d) * u * v);
    }

    public static double Fbm(double x, double y, int octaves, int seed)
    {
        double sum = 0, amplitude = 0.5, total = 0, frequency = 1;
        for (var i = 0; i < octaves; i++)
        {
            sum += amplitude * Value(x * frequency, y * frequency, seed + (i * 31));
            total += amplitude;
            amplitude *= 0.5;
            frequency *= 2.03;
        }

        return sum / total;
    }

    public static double Ridge(double x, int octaves, int seed)
    {
        double sum = 0, amplitude = 0.5, total = 0, frequency = 1;
        for (var i = 0; i < octaves; i++)
        {
            var n = Value(x * frequency, 0.37 * i, seed + (i * 17));
            sum += amplitude * (1 - Math.Abs((2 * n) - 1));
            total += amplitude;
            amplitude *= 0.5;
            frequency *= 2.1;
        }

        return sum / total;
    }

    public static double Smooth(double t)
    {
        var c = Math.Clamp(t, 0, 1);
        return c * c * (3 - (2 * c));
    }

    public static double SmoothStep(double edge0, double edge1, double x) => Smooth((x - edge0) / (edge1 - edge0));
}
