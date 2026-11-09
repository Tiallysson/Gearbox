using Gearbox.Interop.Shell;

namespace Gearbox.Modules.Notch.Native;

internal static class AppIconQueue
{
    private const int MaxConcurrent = 2;

    private static readonly SemaphoreSlim s_gate = new(MaxConcurrent);

    public static Task<ShellIcon?> GetAsync(string parsingName, int size) =>
        RunAsync(() => InstalledApps.TryGetIcon(parsingName, size));

    public static Task<ShellIcon?> GetFileAsync(string filePath, int size) =>
        RunAsync(() => InstalledApps.TryGetFileIcon(filePath, size));

    private static async Task<ShellIcon?> RunAsync(Func<ShellIcon?> read)
    {
        await s_gate.WaitAsync();
        try
        {
            return await Task.Run(read);
        }
        finally
        {
            s_gate.Release();
        }
    }
}
