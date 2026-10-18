namespace Gearbox.Modules.Wallpaper.Logic;

public enum MediaKind
{
    Unsupported,
    Image,
    Video,
}

public static class MediaKinds
{
    public static readonly IReadOnlyList<string> ImageExtensions = (string[])[".jpg", ".jpeg", ".png", ".bmp", ".webp"];

    public static readonly IReadOnlyList<string> VideoExtensions = (string[])[".mp4", ".webm", ".mkv"];

    /// <summary>Tipo pelo nome do arquivo (extensão, sem diferenciar maiúsculas).</summary>
    public static MediaKind Detect(string? path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return MediaKind.Unsupported;
        }

        if (ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return MediaKind.Image;
        }

        return VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ? MediaKind.Video : MediaKind.Unsupported;
    }
}
