using Gearbox.Interop.Geometry;
using Gearbox.Modules.Wallpaper.UI.Scenes;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.Wallpaper.Native;

internal sealed class SceneWallpaperWindow : IAnimatedWallpaper
{
    private readonly WallpaperScene _scene;
    private readonly DesktopHostWindow _host;
    private bool _paused;
    private bool _disposed;

    public SceneWallpaperWindow(string sceneId, PixelRect bounds, ILogger logger)
    {
        Source = sceneId;
        _scene = SceneFactory.Create(sceneId);
        try
        {
            _host = new DesktopHostWindow(_scene.Root, bounds, logger);
        }
        catch
        {
            _scene.Dispose();
            throw;
        }

        _scene.SetAnimating(true);
    }

    public string Source { get; }

    public bool SetPaused(bool paused)
    {
        if (_disposed || paused == _paused)
        {
            return false;
        }

        _paused = paused;
        _scene.SetAnimating(!paused);
        return true;
    }

    public void SetBounds(PixelRect bounds) => _host.SetBounds(bounds);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scene.Dispose();
        _host.Dispose();
    }
}
