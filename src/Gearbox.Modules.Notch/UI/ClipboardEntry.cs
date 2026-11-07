using System.Globalization;
using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Gearbox.Modules.Notch.UI;

public sealed class ClipboardEntry
{
    private ClipboardEntry(ClipboardItem item, ImageSource? thumbnail)
    {
        Id = item.Id;
        Preview = PreviewFor(item);
        Time = item.CopiedAt.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
        IsMedia = item.Kind != ClipboardItemKind.Text;
        Thumbnail = thumbnail;
    }

    public long Id { get; }

    public string Preview { get; }

    public string Time { get; }

    public bool IsMedia { get; }

    public ImageSource? Thumbnail { get; }

    public string CopyLabel => ModuleStrings.Format("ClipboardCopyFormat", Preview);

    public string RemoveLabel => ModuleStrings.Get("ClipboardRemove");

    internal static ClipboardEntry Create(ClipboardItem item, IRandomAccessStream? image, Action<long, Exception> thumbnailFailed)
    {
        if (image is null)
        {
            return new ClipboardEntry(item, null);
        }

        var thumbnail = new SoftwareBitmapSource();
        _ = LoadThumbnailAsync(item.Id, thumbnail, image, thumbnailFailed);
        return new ClipboardEntry(item, thumbnail);
    }

    private static async Task LoadThumbnailAsync(long id, SoftwareBitmapSource thumbnail, IRandomAccessStream image, Action<long, Exception> thumbnailFailed)
    {
        using (image)
        {
            try
            {
                var decoder = await BitmapDecoder.CreateAsync(image);
                var scale = ClipboardThumbnail.Scale(decoder.OrientedPixelWidth, decoder.OrientedPixelHeight);
                var transform = new BitmapTransform
                {
                    ScaledWidth = ClipboardThumbnail.Scaled(decoder.PixelWidth, scale),
                    ScaledHeight = ClipboardThumbnail.Scaled(decoder.PixelHeight, scale),
                    InterpolationMode = BitmapInterpolationMode.Fant,
                };
                var bitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.DoNotColorManage);
                await thumbnail.SetBitmapAsync(bitmap);
            }
            catch (Exception ex)
            {
                thumbnailFailed(id, ex);
            }
        }
    }

    private static string PreviewFor(ClipboardItem item)
    {
        switch (item.Kind)
        {
            case ClipboardItemKind.Text:
                return ClipboardPreview.ForText(item.Text) ?? string.Empty;

            case ClipboardItemKind.Files:
                var paths = ClipboardPreview.FilePaths(item.Text);
                var first = paths.Count > 0 ? ClipboardPreview.FileName(paths[0]) : string.Empty;
                return paths.Count > 1 ? ModuleStrings.Format("ClipboardFilesFormat", first, paths.Count - 1) : first;

            default:
                return ModuleStrings.Get("ClipboardImage");
        }
    }
}
