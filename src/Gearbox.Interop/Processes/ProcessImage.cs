using Windows.Win32;
using Windows.Win32.System.Threading;

namespace Gearbox.Interop.Processes;

internal static class ProcessImage
{
    public static string? TryGetPath(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process.IsInvalid)
        {
            return null;
        }

        Span<char> buffer = stackalloc char[1024];
        var size = (uint)buffer.Length;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref size)
            ? buffer[..(int)size].ToString()
            : null;
    }
}
