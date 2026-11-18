using Gearbox.Interop.Geometry;

namespace Gearbox.Modules.Wallpaper.Services.Windowing;

internal interface IAnimatedWallpaper : IDisposable
{
    string Source { get; }

    bool SetPaused(bool paused);

    void SetBounds(PixelRect bounds);
}
