namespace Gearbox.Modules.Notch.Logic;

internal static class ClipboardReadErrors
{
    private const int CantOpenClipboard = unchecked((int)0x800401D0);
    private const int InvalidFormat = unchecked((int)0x80040064);
    private const int InvalidClipboardFormat = unchecked((int)0x8004006A);
    private const int RpcFacilityMask = unchecked((int)0xFFFF0000);
    private const int RpcFacility = unchecked((int)0x80010000);

    private static readonly TimeSpan[] s_retryDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(150),
        TimeSpan.FromMilliseconds(400),
        TimeSpan.FromMilliseconds(1000),
    ];

    public static int MaxRetries => s_retryDelays.Length;

    public static bool IsTransient(int hresult) =>
        hresult is CantOpenClipboard or InvalidFormat or InvalidClipboardFormat
        || (hresult & RpcFacilityMask) == RpcFacility;

    public static TimeSpan? RetryDelay(int failedAttempts) =>
        failedAttempts >= 1 && failedAttempts <= s_retryDelays.Length ? s_retryDelays[failedAttempts - 1] : null;
}
