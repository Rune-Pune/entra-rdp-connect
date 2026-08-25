namespace EntraRdpConnect.Core.Configuration;

/// <summary>
/// Hele app-konfigurasjonen — alt som trengs for å koble til, og ingenting annet. Lastes fra en
/// lokal, ikke-innsjekket fil (~/.config/entra-rdp-connect/config.json) og kan redigeres i
/// Innstillinger-fanen. Defaults her matcher config.sample.json og gjør typen brukbar i tester
/// uten fil på disk.
/// </summary>
public sealed record AppConfig
{
    public VpnConfig Vpn { get; init; } = new();
    public RdpConfig Rdp { get; init; } = new();
}

public sealed record VpnConfig
{
    /// <summary>WireGuard-interface, f.eks. wg0work — navnet på .conf-fila i /etc/wireguard.
    /// Adapteren kjører `uwg-quick up/down &lt;interface&gt;`.</summary>
    public string Interface { get; init; } = "";

    /// <summary>Hvor lenge vi venter på handshake (push godkjent på mobil) før vi gir opp.</summary>
    public int HandshakeTimeoutSeconds { get; init; } = 45;
}

public sealed record RdpConfig
{
    /// <summary>NAVN, ikke IP — FreeRDP kapper IP ved første punktum og feiler mot Entra.</summary>
    public string Host { get; init; } = "";

    /// <summary>Adressen navnet skal peke på. Brukes bare hvis navnet ikke allerede slås opp,
    /// og appen tilbyr å legge det i /etc/hosts.</summary>
    public string HostIp { get; init; } = "";

    public int Port { get; init; } = 3389;

    /// <summary>Entra-UPN, f.eks. someone@example.com.</summary>
    public string User { get; init; } = "";

    /// <summary>Argumenter til xfreerdp3. /sec:aad er eneste vei inn på Entra-joined maskiner;
    /// /cert:ignore fordi Entras P2P-sertifikat roterer hver ~5. time.</summary>
    public IReadOnlyList<string> ExtraArgs { get; init; } =
        ["/sec:aad", "/cert:ignore", "+clipboard", "/dynamic-resolution"];
}
