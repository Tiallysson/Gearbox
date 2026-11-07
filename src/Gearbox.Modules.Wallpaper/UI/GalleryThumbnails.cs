using System.Runtime.InteropServices.WindowsRuntime;
using Gearbox.Modules.Wallpaper.Logic.Gallery;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Gearbox.Modules.Wallpaper.UI;

internal static class GalleryThumbnails
{
    public static async Task<ImageSource> RenderAsync(GalleryItem item, int width, int height)
    {
        var pixels = await Task.Run(() => StaticWallpaperRenderer.Render(item.Id, width, height));
        var bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        var source = new SoftwareBitmapSource();
        await source.SetBitmapAsync(bitmap);
        return source;
    }
}
