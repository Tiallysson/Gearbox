using Gearbox.Core;
using Gearbox.Modules.Wallpaper.Logic.Gallery;
using Windows.Graphics.Imaging;

namespace Gearbox.Modules.Wallpaper.Native;

public sealed class GalleryImageCache(GearboxPaths paths)
{
    private readonly string _directory = Path.Combine(paths.CacheDirectory, "wallpapers");
    private readonly Dictionary<string, Task<string>> _pending = [];

    public string? TryGetPath(GalleryItem item, int width, int height)
    {
        var path = PathFor(item, width, height);
        return File.Exists(path) ? path : null;
    }

    public Task<string> EnsureAsync(GalleryItem item, int width, int height)
    {
        var path = PathFor(item, width, height);
        if (File.Exists(path))
        {
            return Task.FromResult(path);
        }

        if (_pending.TryGetValue(path, out var running))
        {
            return running;
        }

        var task = RenderAsync(item, width, height, path);
        _pending[path] = task;
        _ = task.ContinueWith(_ => _pending.Remove(path), TaskScheduler.FromCurrentSynchronizationContext());
        return task;
    }

    public void Prune(IEnumerable<string> keep)
    {
        if (!Directory.Exists(_directory))
        {
            return;
        }

        var kept = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);
        var pending = new HashSet<string>(_pending.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(_directory))
        {
            var target = file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
            if (kept.Contains(file) || pending.Contains(target))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private string PathFor(GalleryItem item, int width, int height) =>
        Path.Combine(_directory, GalleryCatalog.CacheFileName(item, width, height));

    private async Task<string> RenderAsync(GalleryItem item, int width, int height, string path)
    {
        var png = await Task.Run(async () =>
        {
            var pixels = StaticWallpaperRenderer.Render(item.Id, width, height);
            return await PngEncoder.EncodeAsync(pixels, width, height, BitmapAlphaMode.Ignore);
        });
        Directory.CreateDirectory(_directory);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, png);
        File.Move(temp, path, overwrite: true);
        return path;
    }
}
