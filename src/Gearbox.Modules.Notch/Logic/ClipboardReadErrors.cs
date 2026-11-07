namespace Gearbox.Modules.Notch.Logic;

internal static class ClipboardReadErrors
{
    private const int CantOpenClipboard = unchecked((int)0x800401D0);
    private const int InvalidFormat = unchecked((int)0x80040064);
    private const int InvalidClipboardFormat = unchecked((int)0x8004006A);
    private const int RpcFacilityMask = unchecked((int)0xFFFF0000);
    private const int RpcFacility = unchecked((int)0x80010000);

    public static bool IsTransient(int hresult) =>
        hresult is CantOpenClipboard or InvalidFormat or InvalidClipboardFormat
        || (hresult & RpcFacilityMask) == RpcFacility;
}
