using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Gearbox.Interop.Shell;

public readonly record struct InstalledApp(string Name, string ParsingName);

public sealed record ShellIcon(int Width, int Height, byte[] Bgra);

public static class InstalledApps
{
    private const string AppsFolderPrefix = "shell:AppsFolder\\";

    public static string LaunchTarget(string parsingName) => AppsFolderPrefix + parsingName;

    public static unsafe IReadOnlyList<InstalledApp> Enumerate()
    {
        var apps = new List<InstalledApp>();
        var folderId = PInvoke.FOLDERID_AppsFolder;
        var itemId = typeof(IShellItem).GUID;
        PInvoke.SHGetKnownFolderItem(&folderId, KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT, HANDLE.Null, &itemId, out var folderObject).ThrowOnFailure();
        var folder = (IShellItem)folderObject;
        folder.BindToHandler<IEnumShellItems>(null, PInvoke.BHID_EnumItems, out var items);
        var batch = new IShellItem[1];
        while (true)
        {
            uint fetched = 0;
            items.Next(1, batch, &fetched);
            if (fetched == 0)
            {
                break;
            }

            var name = DisplayName(batch[0], SIGDN.SIGDN_NORMALDISPLAY);
            var parsing = DisplayName(batch[0], SIGDN.SIGDN_PARENTRELATIVEPARSING);
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(parsing))
            {
                apps.Add(new InstalledApp(name, parsing));
            }
        }

        return apps;
    }

    public static unsafe ShellIcon? TryGetIcon(string parsingName, int size)
    {
        try
        {
            var factoryId = typeof(IShellItemImageFactory).GUID;
            object factoryObject;
            fixed (char* path = LaunchTarget(parsingName))
            {
                PInvoke.SHCreateItemFromParsingName(path, null, &factoryId, out factoryObject).ThrowOnFailure();
            }

            var factory = (IShellItemImageFactory)factoryObject;
            factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_RESIZETOFIT | SIIGBF.SIIGBF_ICONONLY, out var bitmap);
            using (bitmap)
            {
                return ReadPixels((HBITMAP)bitmap.DangerousGetHandle(), size);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return null;
        }
    }

    private static unsafe ShellIcon? ReadPixels(HBITMAP bitmap, int size)
    {
        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = size;
        info.bmiHeader.biHeight = -size;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = 0;

        var pixels = new byte[size * size * 4];
        var dc = PInvoke.GetDC(HWND.Null);
        try
        {
            fixed (byte* buffer = pixels)
            {
                var lines = PInvoke.GetDIBits(dc, bitmap, 0, (uint)size, buffer, &info, DIB_USAGE.DIB_RGB_COLORS);
                return lines == 0 ? null : new ShellIcon(size, size, pixels);
            }
        }
        finally
        {
            _ = PInvoke.ReleaseDC(HWND.Null, dc);
        }
    }

    private static unsafe string? DisplayName(IShellItem item, SIGDN kind)
    {
        try
        {
            item.GetDisplayName(kind, out var name);
            try
            {
                return name.ToString();
            }
            finally
            {
                PInvoke.CoTaskMemFree(name);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return null;
        }
    }
}
