namespace Gearbox.Modules.Notch.Logic;

public enum NotchPage
{
    Media,
    Controls,
    Devices,
    Storage,
    System,
    Clipboard,
    Links,
    Mixer,
    MixerDevices,
    Network,
}

public static class PageTransitions
{
    public static bool IsDrillIn(NotchPage from, NotchPage to) => (from, to) switch
    {
        (NotchPage.Controls, NotchPage.Devices) => true,
        (NotchPage.Controls, NotchPage.Mixer) => true,
        (NotchPage.Controls, NotchPage.Network) => true,
        (NotchPage.Mixer, NotchPage.MixerDevices) => true,
        _ => false,
    };
}
