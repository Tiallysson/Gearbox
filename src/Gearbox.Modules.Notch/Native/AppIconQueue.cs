using Gearbox.Interop.Shell;

namespace Gearbox.Modules.Notch.Native;

internal static class AppIconQueue
{
    private const int MaxConcurrent = 2;

    private static readonly SemaphoreSlim s_gate = new(MaxConcurrent);

    public static async Task<ShellIcon?> GetAsync(string parsingName, int size)
    {
        await s_gate.WaitAsync();
        try
        {
            return await Task.Run(() => InstalledApps.TryGetIcon(parsingName, size));
        }
        finally
        {
            s_gate.Release();
        }
    }
}
