using Gearbox.Modules.Wallpaper.Logic.Gallery;

namespace Gearbox.Modules.Wallpaper.Logic;

public enum WallpaperProblem
{
    None,
    FileNotFound,
    UnsupportedFormat,
    PlaybackFailed,
    OriginalNotRestorable,
    RenderFailed,
}

public readonly record struct GalleryImage(string? Path, bool Failed)
{
    public static GalleryImage Pending => default;

    public static GalleryImage FailedToRender => new(null, true);

    public static GalleryImage Ready(string path) => new(path, false);
}

public sealed record WallpaperAssignment(string DevicePath, string? Path, MediaKind Kind, string? GalleryId, WallpaperProblem Problem)
{
    public bool KeepsOriginal => Path is null && Kind != MediaKind.Pending;

    public bool IsPending => Kind == MediaKind.Pending;

    public bool IsGallery => GalleryId is not null;
}

public static class WallpaperResolver
{
    public static IReadOnlyList<WallpaperAssignment> Resolve(
        WallpaperSettings settings,
        IEnumerable<string> devicePaths,
        Func<string, bool> fileExists,
        IReadOnlySet<string>? failedVideos = null,
        Func<GalleryItem, string, GalleryImage>? galleryImage = null)
    {
        failedVideos ??= new HashSet<string>();
        var gallery = GalleryCatalog.Find(settings.GalleryWallpaperId);
        return devicePaths
            .Select(devicePath => gallery is null
                ? ResolveFile(settings, devicePath, fileExists, failedVideos)
                : ResolveGallery(gallery, devicePath, galleryImage))
            .ToArray();
    }

    private static WallpaperAssignment ResolveGallery(GalleryItem item, string devicePath, Func<GalleryItem, string, GalleryImage>? galleryImage)
    {
        if (item.IsInteractive)
        {
            return new WallpaperAssignment(devicePath, item.Id, MediaKind.Scene, item.Id, WallpaperProblem.None);
        }

        var image = galleryImage?.Invoke(item, devicePath) ?? GalleryImage.Pending;
        if (image.Path is not null)
        {
            return new WallpaperAssignment(devicePath, image.Path, MediaKind.Image, item.Id, WallpaperProblem.None);
        }

        return image.Failed
            ? new WallpaperAssignment(devicePath, null, MediaKind.Unsupported, item.Id, WallpaperProblem.RenderFailed)
            : new WallpaperAssignment(devicePath, null, MediaKind.Pending, item.Id, WallpaperProblem.None);
    }

    private static WallpaperAssignment ResolveFile(
        WallpaperSettings settings,
        string devicePath,
        Func<string, bool> fileExists,
        IReadOnlySet<string> failedVideos)
    {
        var chosen = ChoiceFor(settings, devicePath);
        if (string.IsNullOrWhiteSpace(chosen))
        {
            return new WallpaperAssignment(devicePath, null, MediaKind.Unsupported, null, WallpaperProblem.None);
        }

        var kind = MediaKinds.Detect(chosen);
        var problem = kind switch
        {
            MediaKind.Unsupported => WallpaperProblem.UnsupportedFormat,
            _ when !fileExists(chosen) => WallpaperProblem.FileNotFound,
            MediaKind.Video when failedVideos.Contains(chosen) => WallpaperProblem.PlaybackFailed,
            _ => WallpaperProblem.None,
        };

        return problem == WallpaperProblem.None
            ? new WallpaperAssignment(devicePath, chosen, kind, null, WallpaperProblem.None)
            : new WallpaperAssignment(devicePath, null, MediaKind.Unsupported, null, problem);
    }

    private static string? ChoiceFor(WallpaperSettings settings, string devicePath)
    {
        if (!settings.SameForAllMonitors)
        {
            foreach (var (key, value) in settings.Monitors)
            {
                if (string.Equals(key, devicePath, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value?.Path))
                {
                    return value.Path;
                }
            }
        }

        return settings.Default?.Path;
    }
}
