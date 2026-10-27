namespace Gearbox.Interop;

/// <summary>Operações de wallpaper usadas pela restauração; permite testar falhas sem mudar o desktop.</summary>
public interface IDesktopWallpaperBackend
{
    bool HasSlideshow { get; }

    string? GetWallpaper(string devicePath);

    void SetWallpaper(string devicePath, string imagePath);

    WallpaperFit GetFit();

    void SetFit(WallpaperFit fit);
}
