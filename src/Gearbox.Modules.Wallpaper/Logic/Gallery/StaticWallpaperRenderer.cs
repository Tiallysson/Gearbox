namespace Gearbox.Modules.Wallpaper.Logic.Gallery;

public static class StaticWallpaperRenderer
{
    private const double DitherAmount = 1.5 / 255.0;

    public static byte[] Render(string id, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var shader = CreateShader(id, width, height);
        var pixels = new byte[width * height * 4];
        Parallel.For(0, height, y =>
        {
            var row = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                var color = shader.At(x, y);
                var dither = (Noise.Hash(x, y, 7919) - 0.5) * DitherAmount;
                var offset = row + (x * 4);
                pixels[offset] = ToByte(color.B + dither);
                pixels[offset + 1] = ToByte(color.G + dither);
                pixels[offset + 2] = ToByte(color.R + dither);
                pixels[offset + 3] = 255;
            }
        });
        return pixels;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);

    private static IShader CreateShader(string id, int width, int height) => id switch
    {
        GalleryCatalog.AbstractGradient => new AbstractGradientShader(width, height),
        GalleryCatalog.DarkMinimal => new DarkMinimalShader(width, height),
        GalleryCatalog.Aurora => new AuroraShader(width, height),
        GalleryCatalog.Geometric => new GeometricShader(width, height),
        GalleryCatalog.Nature => new NatureShader(width, height),
        _ => throw new ArgumentException($"'{id}' não é um wallpaper estático da galeria.", nameof(id)),
    };

    private interface IShader
    {
        Rgb At(int x, int y);
    }

    private abstract class Shader(int width, int height) : IShader
    {
        protected int Height { get; } = height;

        protected double Aspect { get; } = width / (double)height;

        protected double Pixel { get; } = 1.0 / height;

        public Rgb At(int x, int y) => Shade(x / (double)Height, y / (double)Height, x, y);

        protected abstract Rgb Shade(double nx, double ny, int x, int y);

        protected static double Grain(int x, int y, double amount) => (Noise.Hash(x, y, 104729) - 0.5) * amount;

        protected double Vignette(double nx, double ny, double strength)
        {
            var dx = (nx / Aspect) - 0.5;
            var dy = ny - 0.5;
            return 1 - (strength * Noise.SmoothStep(0.25, 0.95, Math.Sqrt((dx * dx) + (dy * dy)) * 1.4));
        }
    }

    private sealed class AbstractGradientShader : Shader
    {
        private static readonly Rgb s_base = Rgb.Hex(0x120F2B);

        private readonly (double X, double Y, double Radius, Rgb Color)[] _blobs;

        public AbstractGradientShader(int width, int height)
            : base(width, height)
        {
            _blobs =
            [
                (0.10 * Aspect, 0.15, 0.42, Rgb.Hex(0x2B145E)),
                (0.88 * Aspect, 0.10, 0.36, Rgb.Hex(0xFF5E62)),
                (0.58 * Aspect, 0.58, 0.34, Rgb.Hex(0xE0457B)),
                (0.22 * Aspect, 0.95, 0.42, Rgb.Hex(0x0F1E5C)),
                (0.94 * Aspect, 0.88, 0.34, Rgb.Hex(0xFFB37A)),
                (0.38 * Aspect, 0.32, 0.30, Rgb.Hex(0x7B3CF0)),
                (0.72 * Aspect, 0.30, 0.22, Rgb.Hex(0xFF8FB1)),
            ];
        }

        protected override Rgb Shade(double nx, double ny, int x, int y)
        {
            var qx = Noise.Fbm(nx * 1.2, ny * 1.2, 4, 11) - 0.5;
            var qy = Noise.Fbm((nx * 1.2) + 5.2, (ny * 1.2) + 1.3, 4, 23) - 0.5;
            var wx = nx + (0.55 * Noise.Fbm((nx * 1.6) + (2.4 * qx), (ny * 1.6) + (2.4 * qy), 4, 29) - 0.27);
            var wy = ny + (0.55 * Noise.Fbm((nx * 1.6) + (2.4 * qy) + 3.1, (ny * 1.6) + (2.4 * qx) + 7.7, 4, 31) - 0.27);
            var total = 0.05;
            var color = s_base * 0.05;
            foreach (var (bx, by, radius, blob) in _blobs)
            {
                var dx = wx - bx;
                var dy = wy - by;
                var weight = Math.Pow(Math.Exp(-((dx * dx) + (dy * dy)) / (radius * radius)), 1.6);
                color += blob * weight;
                total += weight;
            }

            color *= 1 / total;
            var fold = Noise.Fbm((wx * 2.6) + 1.7, (wy * 2.6) - 0.4, 4, 37);
            var sheen = (Noise.SmoothStep(0.52, 0.70, fold) - Noise.SmoothStep(0.70, 0.86, fold)) * 0.09;
            var shade = Noise.SmoothStep(0.45, 0.25, fold) * 0.10;
            var grain = Grain(x, y, 0.02);
            return (color * (1 - shade) * Vignette(nx, ny, 0.25)) + new Rgb(sheen + grain, sheen + grain, sheen + grain);
        }
    }

    private sealed class DarkMinimalShader(int width, int height) : Shader(width, height)
    {
        private static readonly Rgb s_top = Rgb.Hex(0x101218);
        private static readonly Rgb s_bottom = Rgb.Hex(0x06070A);
        private static readonly Rgb s_line = Rgb.Hex(0xDCE3FF);
        private static readonly Rgb s_glow = Rgb.Hex(0x4D5DFF);
        private static readonly Rgb s_warm = Rgb.Hex(0xFF9B6A);

        protected override Rgb Shade(double nx, double ny, int x, int y)
        {
            var background = Rgb.Mix(s_top, s_bottom, Noise.Smooth(ny)) * Vignette(nx, ny, 0.45);
            var cx = 0.62 * Aspect;
            const double cy = 1.32;
            const double radius = 1.0;
            var dx = nx - cx;
            var dy = ny - cy;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            var edge = distance - radius;
            if (edge < 0)
            {
                background *= 0.55 + (0.45 * Noise.SmoothStep(-0.25, 0, edge));
            }

            var fade = Noise.SmoothStep(0.02 * Aspect, 0.42 * Aspect, nx) * (1 - Noise.SmoothStep(0.78 * Aspect, 1.04 * Aspect, nx));
            var line = Math.Exp(-Math.Pow(edge / (1.6 * Pixel), 2)) * 0.95;
            var glow = (Math.Exp(-Math.Abs(edge) / 0.035) * 0.32) + (Math.Exp(-Math.Max(edge, 0) / 0.22) * 0.10 * Noise.SmoothStep(0.25, -0.02, edge));
            var warmth = Math.Exp(-Math.Pow((nx - (0.70 * Aspect)) / 0.12, 2)) * 0.6;
            var light = (Rgb.Mix(s_line, s_warm, warmth * 0.5) * (line * fade)) + (Rgb.Mix(s_glow, s_warm, warmth) * (glow * fade));
            var grain = Grain(x, y, 0.012);
            return background + light + new Rgb(grain, grain, grain);
        }
    }

    private sealed class AuroraShader : Shader
    {
        private static readonly Rgb s_green = Rgb.Hex(0x3DFFB2);
        private static readonly Rgb s_teal = Rgb.Hex(0x22C8D8);
        private static readonly Rgb s_violet = Rgb.Hex(0x8C63FF);
        private static readonly Rgb s_mountain = Rgb.Hex(0x03060B);

        private readonly double[] _curtain;
        private readonly double[] _secondCurtain;
        private readonly double[] _mask;
        private readonly double[] _ridge;

        public AuroraShader(int width, int height)
            : base(width, height)
        {
            _curtain = new double[width];
            _secondCurtain = new double[width];
            _mask = new double[width];
            _ridge = new double[width];
            for (var x = 0; x < width; x++)
            {
                var nx = x / (double)height;
                var relative = nx / Aspect;
                _curtain[x] = 0.40 + (0.13 * Math.Sin((relative * 5.2) + 0.6)) + (0.20 * (Noise.Fbm(nx * 1.1, 0.5, 4, 101) - 0.5));
                _secondCurtain[x] = 0.52 + (0.08 * Math.Sin((relative * 3.4) + 2.1)) + (0.16 * (Noise.Fbm((nx * 1.4) + 9.4, 1.5, 4, 131) - 0.5));
                _mask[x] = Noise.SmoothStep(0.25, 0.70, Noise.Fbm(nx * 1.5, 2.5, 3, 151)) * (0.35 + (0.65 * Math.Exp(-Math.Pow((relative - 0.42) / 0.33, 2))));
                _ridge[x] = 0.80 - (0.09 * Noise.Ridge(nx * 1.8, 5, 171)) - (0.02 * Noise.Fbm(nx * 12, 3.5, 3, 191));
            }
        }

        protected override Rgb Shade(double nx, double ny, int x, int y)
        {
            var sky = Rgb.Gradient(ny, (0.0, Rgb.Hex(0x03050F)), (0.45, Rgb.Hex(0x081428)), (0.80, Rgb.Hex(0x0C2B3C)));
            sky += Stars(x, y) * Noise.SmoothStep(0.75, 0.2, ny);
            sky += Curtain(nx, ny, _curtain[x], 0.95, 0) * _mask[x];
            sky += Curtain(nx, ny, _secondCurtain[x], 0.45, 3) * (1 - (_mask[x] * 0.5));
            if (ny <= _ridge[x])
            {
                var horizonGlow = Math.Exp(-(_ridge[x] - ny) / 0.05) * 0.12 * _mask[x];
                return sky + (s_teal * horizonGlow) + new Rgb(Grain(x, y, 0.01), Grain(x, y, 0.01), Grain(x, y, 0.01));
            }

            var rim = Math.Exp(-(ny - _ridge[x]) / (2.5 * Pixel)) * 0.18 * _mask[x];
            return s_mountain + (s_green * rim) + (Rgb.Hex(0x0A1620) * Noise.SmoothStep(1.0, 0.8, ny) * 0.4);
        }

        private Rgb Curtain(double nx, double ny, double baseline, double strength, int seed)
        {
            var above = baseline - ny;
            double vertical = above >= 0 ? Math.Exp(-above / 0.20) : Math.Exp(-Math.Pow(above / 0.025, 2));
            if (vertical < 0.002 && Math.Abs(above) > 0.6)
            {
                return default;
            }

            var sway = 0.015 * Math.Sin((ny * 9) + (nx * 3));
            var rays = (0.35 + Math.Pow(Noise.Fbm((nx + sway) * 13, ny * 0.9, 4, 211 + seed), 2.0) * 2.4) * (0.75 + (0.25 * Noise.Fbm(nx * 3, ny * 3, 2, 223 + seed)));
            var height = Math.Clamp(above / 0.32, 0, 1);
            var color = height < 0.35 ? Rgb.Mix(s_green, s_teal, height / 0.35) : Rgb.Mix(s_teal, s_violet, (height - 0.35) / 0.65);
            var diffuse = Math.Exp(-Math.Abs(above) / 0.09) * 0.22;
            return color * (((vertical * rays) + diffuse) * strength * 0.85);
        }

        private Rgb Stars(int x, int y)
        {
            var cell = Math.Max(3, Height / 180);
            var cx = x / cell;
            var cy = y / cell;
            if (Noise.Hash(cx, cy, 307) < 0.985)
            {
                return default;
            }

            var sx = (cx * cell) + (Noise.Hash(cx, cy, 311) * cell);
            var sy = (cy * cell) + (Noise.Hash(cx, cy, 313) * cell);
            var radius = 0.6 + (Noise.Hash(cx, cy, 317) * 0.9 * Math.Max(1, Height / 1080.0));
            var distance = Math.Sqrt(Math.Pow(x - sx, 2) + Math.Pow(y - sy, 2));
            var light = Math.Exp(-Math.Pow(distance / radius, 2)) * (0.4 + (0.6 * Noise.Hash(cx, cy, 331)));
            return new Rgb(light * 0.9, light * 0.95, light);
        }
    }

    private sealed class GeometricShader(int width, int height) : Shader(width, height)
    {
        private static readonly Rgb s_background = Rgb.Hex(0x15171D);
        private static readonly Rgb s_backgroundEnd = Rgb.Hex(0x1E212A);
        private static readonly Rgb s_amber = Rgb.Hex(0xF2A541);
        private static readonly Rgb s_teal = Rgb.Hex(0x2A9D8F);
        private static readonly Rgb s_coral = Rgb.Hex(0xE76F51);
        private static readonly Rgb s_cream = Rgb.Hex(0xF4E9D8);

        protected override Rgb Shade(double nx, double ny, int x, int y)
        {
            var color = Rgb.Mix(s_background, s_backgroundEnd, Noise.Smooth(((nx / Aspect) + ny) * 0.5));
            var grid = Math.Max(GridLine(nx), GridLine(ny));
            color = Rgb.Mix(color, s_cream, grid * 0.035);

            var circle = Circle(nx, ny, 0.60 * Aspect, 0.42, 0.25);
            var square = Box(nx, ny, 0.40 * Aspect, 0.60, 0.17, 0.17, 0.32);
            var triangle = Triangle(nx, ny, 0.79 * Aspect, 0.70, 0.17);

            color *= 1 - (Shadow(Circle(nx - 0.014, ny - 0.022, 0.60 * Aspect, 0.42, 0.25)) * 0.45);
            color *= 1 - (Shadow(Box(nx - 0.014, ny - 0.022, 0.40 * Aspect, 0.60, 0.17, 0.17, 0.32)) * 0.45);
            color *= 1 - (Shadow(Triangle(nx - 0.014, ny - 0.022, 0.79 * Aspect, 0.70, 0.17)) * 0.45);

            color = Rgb.Mix(color, Shade(s_teal, nx, ny), Fill(square) * 0.94);
            color = Rgb.Mix(color, Shade(s_amber, nx, ny), Fill(circle) * 0.92);
            color = Rgb.Mix(color, Shade(s_coral, nx, ny), Fill(triangle) * 0.92);
            color = Rgb.Mix(color, s_cream, Fill(Math.Abs(Circle(nx, ny, 0.60 * Aspect, 0.42, 0.33)) - (0.9 * Pixel)) * 0.55);
            color = Rgb.Mix(color, s_cream, Fill(Circle(nx, ny, 0.24 * Aspect, 0.30, 0.022)));
            color = Rgb.Mix(color, s_cream, Fill(Math.Abs(Box(nx, ny, 0.86 * Aspect, 0.24, 0.06, 0.06, -0.2)) - (0.9 * Pixel)) * 0.4);

            var grain = Grain(x, y, 0.03);
            return color + new Rgb(grain, grain, grain);
        }

        private Rgb Shade(Rgb color, double nx, double ny) =>
            color * (0.92 + (0.16 * (1 - Noise.Smooth((nx / Aspect * 0.6) + (ny * 0.4)))));

        private double Fill(double distance) => Noise.SmoothStep(Pixel, -Pixel, distance);

        private static double Shadow(double distance) => distance <= 0 ? 1 : Math.Exp(-distance / 0.03);

        private double GridLine(double value)
        {
            const double spacing = 0.08;
            var d = Math.Abs(value - (Math.Round(value / spacing) * spacing));
            return Noise.SmoothStep(1.2 * Pixel, 0, d);
        }

        private static double Circle(double x, double y, double cx, double cy, double radius) =>
            Math.Sqrt(Math.Pow(x - cx, 2) + Math.Pow(y - cy, 2)) - radius;

        private static double Box(double x, double y, double cx, double cy, double halfWidth, double halfHeight, double angle)
        {
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            var px = Math.Abs(((x - cx) * cos) + ((y - cy) * sin)) - halfWidth;
            var py = Math.Abs((-(x - cx) * sin) + ((y - cy) * cos)) - halfHeight;
            var outside = Math.Sqrt(Math.Pow(Math.Max(px, 0), 2) + Math.Pow(Math.Max(py, 0), 2));
            return outside + Math.Min(Math.Max(px, py), 0);
        }

        private static double Triangle(double x, double y, double cx, double cy, double size)
        {
            const double k = 1.7320508075688772;
            var px = Math.Abs(x - cx) - size;
            var py = -(y - cy) + (size / k);
            if (px + (k * py) > 0)
            {
                (px, py) = ((px - (k * py)) / 2, ((-k * px) - py) / 2);
            }

            px -= Math.Clamp(px, -2 * size, 0);
            return -Math.Sqrt((px * px) + (py * py)) * Math.Sign(py);
        }
    }

    private sealed class NatureShader : Shader
    {
        private const int Layers = 5;

        private static readonly Rgb s_haze = Rgb.Hex(0xF2A982);
        private static readonly Rgb s_near = Rgb.Hex(0x110D1E);
        private static readonly Rgb s_sun = Rgb.Hex(0xFFF2D6);
        private static readonly Rgb s_bloom = Rgb.Hex(0xFFAE6B);

        private static readonly double[] s_base = [0.60, 0.665, 0.73, 0.80, 0.90];
        private static readonly double[] s_amplitude = [0.07, 0.08, 0.085, 0.09, 0.11];
        private static readonly double[] s_frequency = [1.4, 2.0, 2.6, 3.2, 4.0];

        private readonly double[,] _ridges;
        private readonly double _sunX;

        public NatureShader(int width, int height)
            : base(width, height)
        {
            _sunX = 0.66 * Aspect;
            _ridges = new double[Layers, width];
            for (var x = 0; x < width; x++)
            {
                var nx = x / (double)height;
                for (var k = 0; k < Layers; k++)
                {
                    var ridge = Noise.Ridge((nx * s_frequency[k]) + (k * 7.3), 5, 401 + (k * 13));
                    var swell = Noise.Fbm((nx * s_frequency[k] * 0.35) + k, 0.5, 3, 451 + k);
                    _ridges[k, x] = s_base[k] - (s_amplitude[k] * ((1.5 * Math.Pow(ridge, 1.6)) + (0.9 * swell) - 0.55));
                }
            }
        }

        protected override Rgb Shade(double nx, double ny, int x, int y)
        {
            var sky = Rgb.Gradient(
                ny,
                (0.0, Rgb.Hex(0x241D4A)),
                (0.30, Rgb.Hex(0x6B3C78)),
                (0.48, Rgb.Hex(0xC85A79)),
                (0.60, Rgb.Hex(0xF39A62)),
                (0.66, Rgb.Hex(0xFFD49A)));
            var dx = nx - _sunX;
            var dy = ny - 0.585;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            var sunDisk = Noise.SmoothStep(0.042 + Pixel, 0.042 - Pixel, distance);
            sky = Rgb.Mix(sky, s_sun, sunDisk);
            sky += s_bloom * ((Math.Exp(-distance / 0.07) * 0.45) + (Math.Exp(-distance / 0.32) * 0.22));
            var clouds = Noise.SmoothStep(0.55, 0.85, Noise.Fbm(nx * 2.5, ny * 9, 4, 503)) * Noise.SmoothStep(0.15, 0.45, ny) * Noise.SmoothStep(0.6, 0.45, ny);
            sky = Rgb.Mix(sky, Rgb.Hex(0xFFC0A0), clouds * 0.25);

            var color = sky;
            for (var k = 0; k < Layers; k++)
            {
                var ridge = _ridges[k, x];
                if (ny < ridge)
                {
                    continue;
                }

                var depth = k / (double)(Layers - 1);
                var layer = Rgb.Mix(Rgb.Mix(s_haze, Rgb.Hex(0x8A4A6E), 0.35), s_near, Math.Pow(depth, 0.8));
                var mist = Math.Exp(-(ny - ridge) / (0.03 + (0.03 * depth))) * (1 - depth) * 0.55;
                var rim = Math.Exp(-(ny - ridge) / (1.5 * Pixel)) * Math.Exp(-Math.Pow((nx - _sunX) / 0.28, 2)) * 0.22 * (1 - (depth * 0.7));
                layer = Rgb.Mix(layer, s_haze, mist) + (s_bloom * rim);
                var fog = Noise.Fbm(nx * 3, (ny * 14) + k, 3, 601 + k) * Math.Exp(-(ny - ridge) / 0.08) * 0.18 * (1 - depth);
                color = Rgb.Mix(layer, s_haze, fog);
            }

            var grain = Grain(x, y, 0.014);
            return (color * Vignette(nx, ny, 0.25)) + new Rgb(grain, grain, grain);
        }
    }
}
