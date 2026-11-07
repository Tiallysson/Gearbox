using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using Gearbox.Interop.Shell;
using Gearbox.Modules.Notch.Native;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Gearbox.Modules.Notch.UI;

public sealed partial class AppChoice(InstalledApp app) : ObservableObject
{
    private const int IconSize = 48;
    private bool _iconRequested;

    public string Name => app.Name;

    internal InstalledApp App => app;

    [ObservableProperty]
    public partial ImageSource? Icon { get; set; }

    internal async Task EnsureIconAsync()
    {
        if (_iconRequested)
        {
            return;
        }

        _iconRequested = true;
        if (await AppIconQueue.GetAsync(app.ParsingName, IconSize) is not { } icon)
        {
            return;
        }

        var bitmap = SoftwareBitmap.CreateCopyFromBuffer(icon.Bgra.AsBuffer(), BitmapPixelFormat.Bgra8, icon.Width, icon.Height, BitmapAlphaMode.Premultiplied);
        var source = new SoftwareBitmapSource();
        await source.SetBitmapAsync(bitmap);
        Icon = source;
    }
}
