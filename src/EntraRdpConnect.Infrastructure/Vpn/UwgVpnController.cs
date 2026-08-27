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

        // Teknisk engelsk for logg og feilsøking; brukerens formulering velges ut fra CommandFailure.
        var (failure, reason) = result.ExitCode switch
        {
            126 => (CommandFailure.PrivilegeDeclined, "the password dialog was dismissed or not approved"),
            127 => (CommandFailure.CommandNotFound, "pkexec or uwg-quick was not found"),
            _ => (CommandFailure.Unknown,
                result.CombinedOutput.Trim() is { Length: > 0 } output ? output : "unknown error"),
        };
        throw new VpnCommandException(
            $"'uwg-quick {direction} {_interface}' failed (exit {result.ExitCode}): {reason}",
            failure,
            result.ExitCode);
    }
}
