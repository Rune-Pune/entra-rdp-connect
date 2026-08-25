namespace EntraRdpConnect.Core.Ports;

/// <summary>
/// Port: sjekker om målmaskinens NAVN kan slås opp, og kan eventuelt legge det i /etc/hosts.
///
/// Dette er et reelt krav, ikke en bekvemmelighet: FreeRDP må få et navn (ikke en IP), ellers
/// kapper den adressen ved første punktum og Entra avviser innloggingen.
/// </summary>
public interface IHostResolution
{
    /// <summary>Slår navnet opp via systemets vanlige navneoppslag.</summary>
    Task<bool> CanResolveAsync(string host, CancellationToken ct = default);

    /// <summary>Legger «ip host» i /etc/hosts (krever rettighetshevning).</summary>
    Task AddHostsEntryAsync(string ip, string host, CancellationToken ct = default);
}
