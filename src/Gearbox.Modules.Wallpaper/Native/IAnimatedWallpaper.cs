using Gearbox.Interop.Geometry;

namespace Gearbox.Modules.Wallpaper.Native;

internal interface IAnimatedWallpaper : IDisposable
{
    string Source { get; }

    bool SetPaused(bool paused);

    void SetBounds(PixelRect bounds);
}
