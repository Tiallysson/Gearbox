using System.Diagnostics;
using Gearbox.Interop.Shell;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Native;

internal static class LinkLauncher
{
    public static void Open(QuickLink link)
    {
        if (!link.IsSafe())
        {
            throw new InvalidOperationException("Link inválido ou adulterado.");
        }

        if (link.Kind == QuickLinkKind.Web && QuickLink.TryGetWebTarget(link.Target, out var uri))
        {
            using var browser = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return;
        }

        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var start = new ProcessStartInfo(explorer) { UseShellExecute = false };
        start.ArgumentList.Add(InstalledApps.LaunchTarget(link.Target));
        using var app = Process.Start(start);
    }
}
