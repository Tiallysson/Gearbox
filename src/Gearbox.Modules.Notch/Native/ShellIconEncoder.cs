using System.Runtime.InteropServices.WindowsRuntime;
using Gearbox.Interop.Shell;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Gearbox.Modules.Notch.Native;

internal static class ShellIconEncoder
{
    public static async Task<byte[]> ToPngAsync(ShellIcon icon)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)icon.Width, (uint)icon.Height, 96, 96, icon.Bgra);
        await encoder.FlushAsync();

        var bytes = new byte[stream.Size];
        stream.Seek(0);
        await stream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);
        return bytes;
    }
}
