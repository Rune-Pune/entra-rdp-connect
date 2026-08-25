using System.Net;
using EntraRdpConnect.Core.Ports;
using EntraRdpConnect.Infrastructure.Processes;

namespace EntraRdpConnect.Infrastructure.Net;

/// <summary>
/// Adapter for <see cref="IHostResolution"/>: slår opp navnet med systemets resolver, og kan
/// legge en linje i /etc/hosts via pkexec når navnet ikke finnes i DNS.
/// </summary>
public sealed class HostsFileResolution : IHostResolution
{
    public async Task<bool> CanResolveAsync(string host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
            return addresses.Length > 0;
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or ArgumentException)
        {
            return false;
        }
    }

    public async Task AddHostsEntryAsync(string ip, string host, CancellationToken ct = default)
    {
        var line = BuildEntry(ip, host);

        // tee -a legger til uten å røre eksisterende innhold; pkexec gir passord-dialogen.
        // Kommandoen bygges av validerte verdier, aldri av rå brukertekst.
        var spec = new ProcessSpec("pkexec", "bash", "-c", $"printf '%s\\n' '{line}' >> /etc/hosts");
        var result = await ProcessRunner.RunToCompletionAsync(spec, ct).ConfigureAwait(false);

        if (result.Succeeded)
            return;

        var reason = result.ExitCode switch
        {
            126 => "passord-dialogen ble avbrutt",
            127 => "pkexec ble ikke funnet",
            _ => result.CombinedOutput.Trim() is { Length: > 0 } o ? o : "ukjent feil",
        };
        throw new InvalidOperationException($"Klarte ikke å skrive til /etc/hosts: {reason}");
    }

    /// <summary>Bygger og validerer hosts-linja. Avviser alt som ikke er en ren IP-adresse og et
    /// gyldig vertsnavn, så ingenting kan smugles inn i kommandoen.</summary>
    internal static string BuildEntry(string ip, string host)
    {
        if (!IPAddress.TryParse(ip?.Trim(), out var parsed))
            throw new ArgumentException($"«{ip}» er ikke en gyldig IP-adresse.", nameof(ip));

        var name = host?.Trim() ?? "";
        if (name.Length == 0 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.'))
            throw new ArgumentException($"«{host}» er ikke et gyldig vertsnavn.", nameof(host));

        return $"{parsed}\t{name}";
    }
}
