using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Domain;

namespace EntraRdpConnect.Core.Ports;

/// <summary>
/// Port: én RDP-økt mot en Entra-tilknyttet maskin.
///
/// <paramref name="resolveRedirectUrl"/> er seamen mot fangst-laget: gitt login-URL-en fra
/// xfreerdp3 returnerer den nativeclient?code=-URL-en. Implementasjonen kan være manuell
/// innliming eller automatisk fangst — kontrakten er den samme.
/// </summary>
public interface IRdpSession
{
    Task<int> ConnectAsync(
        RdpConnectionInfo info,
        Func<Uri, CancellationToken, Task<Uri>> resolveRedirectUrl,
        IProgress<RdpPhase>? progress = null,
        CancellationToken ct = default);
}
