using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Core.Application;

/// <summary>
/// Binder hele tilkoblingskjeden: sørg for at VPN-tunnelen er oppe, kjør så RDP-økta med
/// auto-fangst av Entra-koden. Nedkobling av tunnelen er en egen operasjon (default: la stå oppe).
///
/// VPN er valgfritt. Er målmaskinen allerede nåbar — direkte, eller gjennom en tunnel som
/// settes opp utenfor appen — sendes <c>null</c> inn, og tilkoblingen går rett på RDP.
///
/// Testbar med fake VPN-controller + fake RDP-økt — hele flyten uten ekte tunnel eller xfreerdp3.
/// </summary>
public sealed class ConnectionOrchestrator
{
    private readonly VpnConnection? _vpn;
    private readonly IRdpSession _rdp;
    private readonly RdpConnectionInfo _rdpInfo;
    private readonly Func<Uri, CancellationToken, Task<Uri>> _resolveRedirectUrl;

    public ConnectionOrchestrator(
        VpnConnection? vpn,
        IRdpSession rdp,
        RdpConnectionInfo rdpInfo,
        Func<Uri, CancellationToken, Task<Uri>> resolveRedirectUrl)
    {
        _vpn = vpn;
        _rdp = rdp;
        _rdpInfo = rdpInfo;
        _resolveRedirectUrl = resolveRedirectUrl;
    }

    /// <summary>Sant når appen håndterer VPN-tunnelen selv.</summary>
    public bool ManagesVpn => _vpn is not null;

    /// <summary>VPN opp (hvis konfigurert og nødvendig) → RDP med auto-fangst.
    /// Returnerer xfreerdp3 sin exit-kode.</summary>
    public async Task<int> ConnectAsync(
        IProgress<VpnPhase>? vpnProgress = null,
        IProgress<RdpPhase>? rdpProgress = null,
        CancellationToken ct = default)
    {
        if (_vpn is not null)
            await _vpn.EnsureConnectedAsync(vpnProgress, ct).ConfigureAwait(false);
        else
            vpnProgress?.Report(VpnPhase.NotConfigured);

        return await _rdp.ConnectAsync(_rdpInfo, _resolveRedirectUrl, rdpProgress, ct).ConfigureAwait(false);
    }

    /// <summary>Kobler ned VPN-tunnelen (for en «Koble fra alt»-handling i GUI-et).
    /// Gjør ingenting når appen ikke styrer VPN.</summary>
    public Task DisconnectAsync(CancellationToken ct = default)
        => _vpn?.BringDownAsync(ct) ?? Task.CompletedTask;
}
