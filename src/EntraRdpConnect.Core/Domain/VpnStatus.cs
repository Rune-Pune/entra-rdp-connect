namespace EntraRdpConnect.Core.Domain;

/// <summary>
/// Observerbar tilstand for VPN-tunnelen — det vi kan lese rot-fritt fra /sys/class/net.
/// Tunnelen regnes som tilkoblet når interfacet er oppe OG vi har mottatt data, som betyr at
/// handshake fullførte (samme kriterium som den manuelle statusen: "received ikke er 0 B").
/// </summary>
public sealed record VpnStatus
{
    public required bool InterfaceUp { get; init; }

    public long ReceivedBytes { get; init; }

    public bool IsConnected => InterfaceUp && ReceivedBytes > 0;

    public static VpnStatus Down => new() { InterfaceUp = false };
}
