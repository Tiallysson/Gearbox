using System.Globalization;

namespace Gearbox.Modules.Notch.Logic;

/// <summary>Por onde o PC chega à internet.</summary>
public enum NetworkKind
{
    None,
    WiFi,
    Ethernet,

    /// <summary>Celular, VPN, ponte...: a aba mostra o Wi‑Fi normalmente.</summary>
    Other,
}

/// <summary>Conexão com a internet: tipo, nome (perfil do Windows) e velocidade do link, em bits/s (0 = desconhecida).</summary>
public readonly record struct NetworkLink(NetworkKind Kind, string? Name = null, ulong SpeedBitsPerSecond = 0)
{
    public static readonly NetworkLink None = new(NetworkKind.None);
}

public static class NetworkLinks
{
    /// <summary>IANA ifType de Ethernet (ethernetCsmacd).</summary>
    public const uint IanaEthernet = 6;

    public static NetworkKind Classify(bool hasInternetProfile, bool isWlan, uint ianaInterfaceType) =>
        !hasInternetProfile ? NetworkKind.None
        : isWlan ? NetworkKind.WiFi
        : ianaInterfaceType == IanaEthernet ? NetworkKind.Ethernet
        : NetworkKind.Other;

    /// <summary>Velocidade do link como o Windows mostra: "1 Gbps", "2,5 Gbps", "100 Mbps"; vazio se desconhecida.</summary>
    public static string FormatSpeed(ulong bitsPerSecond, IFormatProvider? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return bitsPerSecond switch
        {
            0 => string.Empty,
            >= 1_000_000_000 => string.Format(culture, "{0:0.#} Gbps", bitsPerSecond / 1_000_000_000.0),
            >= 1_000_000 => string.Format(culture, "{0:0.#} Mbps", bitsPerSecond / 1_000_000.0),
            _ => string.Format(culture, "{0:0} Kbps", bitsPerSecond / 1_000.0),
        };
    }
}
