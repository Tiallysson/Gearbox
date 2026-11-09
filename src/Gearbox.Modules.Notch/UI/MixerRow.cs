using CommunityToolkit.Mvvm.ComponentModel;
using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Xaml.Media;

namespace Gearbox.Modules.Notch.UI;

public sealed partial class MixerRow : ObservableObject
{
    public MixerRow(MixerApp app)
    {
        Key = app.Key;
        MuteLabel = ModuleStrings.Get("MixerMuteLabel");
        Update(app);
    }

    public string Key { get; }

    public string MuteLabel { get; }

    public string Placeholder => NotchGlyphs.AppWindow;

    public string? ExecutablePath { get; private set; }

    public string? OutputDeviceId { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double Percent { get; set; }

    [ObservableProperty]
    public partial string VolumeGlyph { get; set; } = NotchGlyphs.VolumeHigh;

    [ObservableProperty]
    public partial double Peak { get; set; }

    [ObservableProperty]
    public partial ImageSource? Icon { get; set; }

    public void Update(MixerApp app)
    {
        Name = app.Name;
        ExecutablePath = app.ExecutablePath;
        OutputDeviceId = app.OutputDeviceId;
        Percent = NotchGlyphs.Percent(app.Volume);
        VolumeGlyph = NotchGlyphs.Volume(app.Volume, app.IsMuted);
    }
}
