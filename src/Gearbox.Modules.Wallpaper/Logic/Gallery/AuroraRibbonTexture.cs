namespace Gearbox.Modules.Wallpaper.Logic.Gallery;

public static class AuroraRibbonTexture
{
    public const int Width = 1024;
    public const int Height = 512;

    private static readonly Rgb s_green = Rgb.Hex(0x3DFFB2);
    private static readonly Rgb s_teal = Rgb.Hex(0x22C8D8);
    private static readonly Rgb s_violet = Rgb.Hex(0x9A6BFF);

    public static byte[] Render(int seed)
    {
        var pixels = new byte[Width * Height * 4];
        var baseline = new double[Width];
        var edge = new double[Width];
        for (var x = 0; x < Width; x++)
        {
            var u = x / (double)Width;
            baseline[x] = 0.72 + (0.10 * Math.Sin((u * Math.PI * 2) + seed)) + (0.12 * (Noise.Fbm(u * 3, seed * 0.7, 4, 701 + seed) - 0.5));
            edge[x] = Noise.SmoothStep(0, 0.18, u) * Noise.SmoothStep(1, 0.82, u);
        }

        Parallel.For(0, Height, y =>
        {
            var v = y / (double)Height;
            for (var x = 0; x < Width; x++)
            {
                var u = x / (double)Width;
                var above = baseline[x] - v;
                var vertical = above >= 0 ? Math.Exp(-above / 0.30) : Math.Exp(-Math.Pow(above / 0.04, 2));
                var sway = 0.01 * Math.Sin((v * 8) + (u * 5) + seed);
                var rays = 0.3 + (Math.Pow(Noise.Fbm((u + sway) * 46, v * 1.1, 4, 733 + seed), 2.0) * 2.6);
                var intensity = Math.Clamp(vertical * rays * edge[x] * 0.95, 0, 1);
                var height = Math.Clamp(above / 0.5, 0, 1);
                var color = height < 0.3 ? Rgb.Mix(s_green, s_teal, height / 0.3) : Rgb.Mix(s_teal, s_violet, (height - 0.3) / 0.7);
                var offset = ((y * Width) + x) * 4;
                pixels[offset] = ToByte(color.B * intensity);
                pixels[offset + 1] = ToByte(color.G * intensity);
                pixels[offset + 2] = ToByte(color.R * intensity);
                pixels[offset + 3] = ToByte(intensity);
            }
        });
        return pixels;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);
}
