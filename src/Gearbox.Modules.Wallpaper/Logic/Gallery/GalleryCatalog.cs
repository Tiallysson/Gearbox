namespace Gearbox.Modules.Wallpaper.Logic.Gallery;

public enum GalleryKind
{
    Static,
    Interactive,
}

public enum GalleryFilter
{
    All,
    Static,
    Interactive,
}

public sealed record GalleryItem(string Id, GalleryKind Kind)
{
    public bool IsInteractive => Kind == GalleryKind.Interactive;
}

public static class GalleryCatalog
{
    public const string AbstractGradient = "abstract-gradient";
    public const string DarkMinimal = "dark-minimal";
    public const string Aurora = "aurora";
    public const string Geometric = "geometric";
    public const string Nature = "nature";
    public const string LivingGradient = "living-gradient";
    public const string AuroraFlow = "aurora-flow";
    public const string Particles = "particles";
    public const string DigitalClock = "digital-clock";
    public const string FluidWaves = "fluid-waves";

    public static IReadOnlyList<GalleryItem> All { get; } = (GalleryItem[])
    [
        new(AbstractGradient, GalleryKind.Static),
        new(DarkMinimal, GalleryKind.Static),
        new(Aurora, GalleryKind.Static),
        new(Geometric, GalleryKind.Static),
        new(Nature, GalleryKind.Static),
        new(LivingGradient, GalleryKind.Interactive),
        new(AuroraFlow, GalleryKind.Interactive),
        new(Particles, GalleryKind.Interactive),
        new(DigitalClock, GalleryKind.Interactive),
        new(FluidWaves, GalleryKind.Interactive),
    ];

    public static GalleryItem? Find(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : All.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<GalleryItem> Filter(GalleryFilter filter) => filter switch
    {
        GalleryFilter.Static => All.Where(i => i.Kind == GalleryKind.Static).ToList(),
        GalleryFilter.Interactive => All.Where(i => i.Kind == GalleryKind.Interactive).ToList(),
        _ => All,
    };

    public static string CacheFileName(GalleryItem item, int width, int height) => $"{item.Id}-{width}x{height}.png";
}
