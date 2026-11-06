using Gearbox.Interop.Desktop;

namespace Gearbox.Modules.Wallpaper.Logic;

public enum WallpaperFitMode
{
    Fill,
    Fit,
    Stretch,
    Center,
}

public static class WallpaperFitModes
{
    public static WallpaperFit ToWallpaperFit(this WallpaperFitMode mode) => mode switch
    {
        WallpaperFitMode.Fit => WallpaperFit.Fit,
        WallpaperFitMode.Stretch => WallpaperFit.Stretch,
        WallpaperFitMode.Center => WallpaperFit.Center,
        _ => WallpaperFit.Fill
    };
}
