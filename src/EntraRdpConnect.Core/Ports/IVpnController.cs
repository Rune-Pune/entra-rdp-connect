using EntraRdpConnect.Core.Domain;

namespace EntraRdpConnect.Core.Ports;

/// <summary>
/// Port: styring av VPN-tunnelen. Tynn med vilje — poll/timeout-logikken bor i applikasjonslaget
/// (<c>VpnConnection</c>), ikke her, så den kan testes uten en ekte tunnel.
/// </summary>
public interface IVpnController
{
    /// <summary>Leser gjeldende tilstand. Skal være rot-fri og billig (egnet for polling).</summary>
    Task<VpnStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Initierer oppkobling. Returnerer når kommandoen er kjørt — handshake fullfører
    /// først når push er godkjent på mobilen, og observeres via <see cref="GetStatusAsync"/>.</summary>
    Task BringUpAsync(CancellationToken ct = default);

    Task BringDownAsync(CancellationToken ct = default);
}
