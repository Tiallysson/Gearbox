using System.Diagnostics;
using Gearbox.Interop.Shell;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Native;

internal static class LinkLauncher
{
    public static void Open(QuickLink link)
    {
        var start = link.Kind == QuickLinkKind.App
            ? new ProcessStartInfo("explorer.exe", InstalledApps.LaunchTarget(link.Target)) { UseShellExecute = true }
            : new ProcessStartInfo(link.Target) { UseShellExecute = true };
        using var process = Process.Start(start);
    }
}
