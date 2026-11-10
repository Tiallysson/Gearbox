using Gearbox.Interop.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Gearbox.Interop.Desktop;

public static class ScreenPixels
{
    public static unsafe byte[]? CaptureBgra(PixelRect area)
    {
        if (area.IsEmpty)
        {
            return null;
        }

        var screen = PInvoke.GetDC(HWND.Null);
        if (screen.IsNull)
        {
            return null;
        }

        var memory = PInvoke.CreateCompatibleDC(screen);
        var bitmap = PInvoke.CreateCompatibleBitmap(screen, area.Width, area.Height);
        try
        {
            if (memory.IsNull || bitmap.IsNull)
            {
                return null;
            }

            var previous = PInvoke.SelectObject(memory, bitmap);
            var copied = PInvoke.BitBlt(memory, 0, 0, area.Width, area.Height, screen, area.Left, area.Top, ROP_CODE.SRCCOPY);
            PInvoke.SelectObject(memory, previous);
            if (!copied)
            {
                return null;
            }

            var info = new BITMAPINFO();
            info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            info.bmiHeader.biWidth = area.Width;
            info.bmiHeader.biHeight = -area.Height;
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;

            var pixels = new byte[area.Width * area.Height * 4];
            fixed (byte* buffer = pixels)
            {
                var lines = PInvoke.GetDIBits(memory, bitmap, 0, (uint)area.Height, buffer, &info, DIB_USAGE.DIB_RGB_COLORS);
                return lines == 0 ? null : pixels;
            }
        }
        finally
        {
            if (!bitmap.IsNull)
            {
                PInvoke.DeleteObject(bitmap);
            }

            if (!memory.IsNull)
            {
                PInvoke.DeleteDC(memory);
            }

            _ = PInvoke.ReleaseDC(HWND.Null, screen);
        }
    }
}
