using System.Net;
using System.Net.Sockets;

namespace Gearbox.Modules.Notch.Logic;

public static class NetworkAddressRules
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 10 ||
                     b[0] == 0 ||
                     (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||
                     (b[0] == 169 && b[1] == 254) ||
                     (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                     (b[0] == 192 && b[1] == 168) ||
                     b[0] >= 224);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.IsIPv6LinkLocal ||
                     address.IsIPv6SiteLocal ||
                     address.IsIPv6Multicast ||
                     (b[0] & 0xFE) == 0xFC);
        }

        return false;
    }

    public static bool AllPublic(IReadOnlyCollection<IPAddress> addresses) =>
        addresses.Count > 0 && addresses.All(IsPublic);
}
