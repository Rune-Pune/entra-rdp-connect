using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Core.Application;

public enum VpnPhase
{
    /// <summary>Appen styrer ikke VPN — målmaskinen antas å være nåbar allerede.</summary>
    NotConfigured,
    AlreadyConnected,
    BringingUp,
    WaitingForHandshake,
    Connected,
    TimedOut,
}

/// <summary>
/// Applikasjonslogikk for å få VPN-tunnelen opp: sjekk om den alt er oppe, ellers reis den og
/// poll til handshake kommer (push godkjent på mobil) — eller time ut med tydelig melding.
///
/// Poll/timeout-logikken bor HER, ikke i adapteren, så «push uteblir → tydelig timeout» kan
/// testes deterministisk med en fake klokke (millisekunder, ikke 45 ekte sekunder).
/// </summary>
public sealed class VpnConnection
{
    private readonly IVpnController _controller;
    private readonly ISystemClock _clock;
    private readonly TimeSpan _handshakeTimeout;
    private readonly TimeSpan _pollInterval;

    public VpnConnection(
        IVpnController controller,
        ISystemClock clock,
        TimeSpan handshakeTimeout,
        TimeSpan pollInterval)
    {
        _controller = controller;
        _clock = clock;
        _handshakeTimeout = handshakeTimeout;
        _pollInterval = pollInterval;
    }

    public async Task<VpnStatus> EnsureConnectedAsync(
        IProgress<VpnPhase>? progress = null,
        CancellationToken ct = default)
    {
        var status = await _controller.GetStatusAsync(ct).ConfigureAwait(false);
        if (status.IsConnected)
        {
            progress?.Report(VpnPhase.AlreadyConnected);
            return status;
        }

        progress?.Report(VpnPhase.BringingUp);
        await _controller.BringUpAsync(ct).ConfigureAwait(false);

        progress?.Report(VpnPhase.WaitingForHandshake);
        var deadline = _clock.UtcNow + _handshakeTimeout;
        while (_clock.UtcNow < deadline)
        {
            await _clock.DelayAsync(_pollInterval, ct).ConfigureAwait(false);

            status = await _controller.GetStatusAsync(ct).ConfigureAwait(false);
            if (status.IsConnected)
            {
                progress?.Report(VpnPhase.Connected);
                return status;
            }
        }

        progress?.Report(VpnPhase.TimedOut);
        throw new VpnHandshakeTimeoutException(_handshakeTimeout);
    }

    public Task BringDownAsync(CancellationToken ct = default)
        => _controller.BringDownAsync(ct);
}
