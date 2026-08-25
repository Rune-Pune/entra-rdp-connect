using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Core.Ports;
using EntraRdpConnect.Infrastructure.Processes;

namespace EntraRdpConnect.Infrastructure.Vpn;

/// <summary>
/// Adapter for <see cref="IVpnController"/> rundt UniFi sin uwg-quick.
///
/// Opp/ned kjøres via pkexec (grafisk passord-dialog, én gang). Status leses ROT-FRITT fra
/// /sys/class/net — så polling under handshake-ventingen ikke spør om passord hvert sekund.
/// </summary>
public sealed class UwgVpnController : IVpnController
{
    private readonly string _interface;

    public UwgVpnController(string interfaceName) => _interface = interfaceName;

    public Task<VpnStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var netDir = $"/sys/class/net/{_interface}";
        if (!Directory.Exists(netDir))
            return Task.FromResult(VpnStatus.Down);

        long received = 0;
        var rxPath = Path.Combine(netDir, "statistics", "rx_bytes");
        if (File.Exists(rxPath) && long.TryParse(File.ReadAllText(rxPath).Trim(), out var value))
            received = value;

        // rx_bytes > 0 betyr at vi har fått svar fra peer'en — dvs. handshake fullførte.
        return Task.FromResult(new VpnStatus { InterfaceUp = true, ReceivedBytes = received });
    }

    public Task BringUpAsync(CancellationToken ct = default) => ElevateAsync("up", ct);

    public Task BringDownAsync(CancellationToken ct = default) => ElevateAsync("down", ct);

    private async Task ElevateAsync(string direction, CancellationToken ct)
    {
        // pkexec setter en sikker PATH som inkluderer /usr/bin, der uwg-quick ligger.
        var spec = new ProcessSpec("pkexec", "uwg-quick", direction, _interface);
        var result = await ProcessRunner.RunToCompletionAsync(spec, ct).ConfigureAwait(false);

        if (result.Succeeded)
            return;

        var reason = result.ExitCode switch
        {
            126 => "passord-dialogen ble avbrutt eller ikke godkjent",
            127 => "pkexec eller uwg-quick ble ikke funnet",
            _ => result.CombinedOutput.Trim() is { Length: > 0 } output ? output : "ukjent feil",
        };
        throw new VpnCommandException(
            $"'uwg-quick {direction} {_interface}' feilet (exit {result.ExitCode}): {reason}",
            result.ExitCode);
    }
}
