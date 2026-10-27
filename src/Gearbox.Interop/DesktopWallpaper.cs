using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Gearbox.Interop;

/// <summary>Como a imagem ocupa o monitor (mesmos valores de <c>DESKTOP_WALLPAPER_POSITION</c>).</summary>
public enum WallpaperFit
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5,
}

/// <summary>Um monitor como o <c>IDesktopWallpaper</c> o enxerga.</summary>
/// <param name="DevicePath">Identificador estável entre reinícios (usado nas configurações).</param>
/// <param name="Bounds">Retângulo do monitor em coordenadas de tela.</param>
public readonly record struct WallpaperMonitor(string DevicePath, PixelRect Bounds);

/// <summary>Wrapper do <c>IDesktopWallpaper</c>: o wallpaper estático do próprio Windows, por monitor.</summary>
public sealed class DesktopWallpaper : IDesktopWallpaperBackend, IDisposable
{
    private IDesktopWallpaper? _api;

    public DesktopWallpaper()
    {
        _api = (IDesktopWallpaper)new Windows.Win32.UI.Shell.DesktopWallpaper();
    }

    private IDesktopWallpaper Api => _api ?? throw new ObjectDisposedException(nameof(DesktopWallpaper));

    /// <summary>Slideshow exige guardar mais que a imagem atual; o Gearbox preserva esse modo.</summary>
    public unsafe bool HasSlideshow
    {
        get
        {
            DESKTOP_SLIDESHOW_STATE state;
            Api.GetStatus(&state);
            return (state & DESKTOP_SLIDESHOW_STATE.DSS_SLIDESHOW) != 0;
        }
    }

    /// <summary>Monitores ativos (os desconectados, que o Windows ainda lista, ficam de fora).</summary>
    public unsafe IReadOnlyList<WallpaperMonitor> GetMonitors()
    {
        Api.GetMonitorDevicePathCount(out var count);
        var monitors = new List<WallpaperMonitor>((int)count);
        for (uint i = 0; i < count; i++)
        {
            PWSTR path;
            Api.GetMonitorDevicePathAt(i, &path);
            var devicePath = TakeString(path);
            if (devicePath is null)
            {
                continue;
            }

            try
            {
                RECT rect;
                fixed (char* id = devicePath)
                {
                    Api.GetMonitorRECT(id, &rect);
                }

                var bounds = new PixelRect(rect.left, rect.top, rect.right, rect.bottom);
                if (!bounds.IsEmpty)
                {
                    monitors.Add(new WallpaperMonitor(devicePath, bounds));
                }
            }
            catch (COMException)
            {
                // Monitor listado mas inativo: GetMonitorRECT falha.
            }
        }

        return monitors;
    }

    /// <summary>Caminho da imagem atual do monitor, ou <c>null</c> (cor sólida, slideshow, Spotlight...).</summary>
    public unsafe string? GetWallpaper(string devicePath)
    {
        try
        {
            PWSTR path;
            fixed (char* id = devicePath)
            {
                Api.GetWallpaper(id, &path);
            }

            var value = TakeString(path);
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public unsafe void SetWallpaper(string devicePath, string imagePath)
    {
        fixed (char* id = devicePath)
        fixed (char* image = imagePath)
        {
            Api.SetWallpaper(id, image);
        }
    }

    public unsafe WallpaperFit GetFit()
    {
        DESKTOP_WALLPAPER_POSITION position;
        Api.GetPosition(&position);
        return (WallpaperFit)(int)position;
    }

    /// <summary>O Windows só tem um "ajuste" para todos os monitores.</summary>
    public void SetFit(WallpaperFit fit) => Api.SetPosition((DESKTOP_WALLPAPER_POSITION)(int)fit);

    /// <summary>Copia uma string alocada pelo COM e a libera.</summary>
    private static unsafe string? TakeString(PWSTR value)
    {
        if (value.Value is null)
        {
            return null;
        }

        var result = value.ToString();
        Marshal.FreeCoTaskMem((nint)value.Value);
        return result;
    }

    public void Dispose()
    {
        if (_api is not null)
        {
            // Nunca FinalReleaseComObject: o objeto do shell é único no processo e o .NET reaproveita o mesmo
            // RCW entre instâncias, então liberar tudo quebraria as outras (ex.: a página e o motor do módulo).
            Marshal.ReleaseComObject(_api);
            _api = null;
        }
    }
}

