using Windows.Win32;
using Windows.Win32.System.SystemInformation;

namespace Gearbox.Interop;

/// <summary>Memória física (RAM), em bytes.</summary>
public readonly record struct MemoryStatus(ulong TotalBytes, ulong AvailableBytes)
{
    public ulong UsedBytes => TotalBytes - Math.Min(AvailableBytes, TotalBytes);
}

public static class SystemMemory
{
    public static unsafe bool TryGetStatus(out MemoryStatus status)
    {
        var info = new MEMORYSTATUSEX { dwLength = (uint)sizeof(MEMORYSTATUSEX) };
        if (!PInvoke.GlobalMemoryStatusEx(&info))
        {
            status = default;
            return false;
        }

        status = new MemoryStatus(info.ullTotalPhys, info.ullAvailPhys);
        return true;
    }
}
