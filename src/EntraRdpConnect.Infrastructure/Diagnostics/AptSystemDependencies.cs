using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Core.Ports;
using EntraRdpConnect.Infrastructure.Processes;

namespace EntraRdpConnect.Infrastructure.Diagnostics;

/// <summary>
/// Adapter for <see cref="ISystemDependencies"/> som installerer manglende verktøy med apt via
/// pkexec (grafisk passord-dialog).
///
/// Sikkerhet: pakkenavnene tas ALDRI fra kalleren. Vi slår opp hver forespurt kommando i vår egen
/// liste og bruker pakkenavnet derfra, så ingen vilkårlig streng kan havne i apt-kommandoen.
/// </summary>
public sealed class AptSystemDependencies : ISystemDependencies
{
    /// <summary>Verktøy som bare trengs når appen selv styrer VPN-tunnelen.</summary>
    private static readonly string[] VpnOnlyTools = ["uwg-quick", "uwg"];

    private readonly bool _managesVpn;

    /// <param name="managesVpn">Er VPN konfigurert? Uten det er UniFi-verktøyene irrelevante,
    /// og skal ikke rapporteres som manglende.</param>
    public AptSystemDependencies(bool managesVpn = true) => _managesVpn = managesVpn;

    public IReadOnlyList<MissingDependency> FindMissing()
        => DependencyChecker.Check()
            .Where(r => !r.Found && r.Dependency.Required)
            .Where(r => _managesVpn || !VpnOnlyTools.Contains(r.Dependency.Command))
            .Select(r => new MissingDependency(r.Dependency.Command, r.Dependency.Purpose, r.Dependency.AptPackage))
            .ToList();

    public async Task InstallAsync(
        IReadOnlyList<MissingDependency> dependencies,
        IProgress<InstallProgress>? log = null,
        CancellationToken ct = default)
    {
        // Oversett kommandonavn -> pakkenavn fra VÅR liste, ikke fra det som ble sendt inn.
        var packages = dependencies
            .Select(d => DependencyChecker.Required.FirstOrDefault(k => k.Command == d.Command)?.AptPackage)
            .Where(p => p is not null)
            .Distinct()
            .ToList();

        if (packages.Count == 0)
        {
            log?.Report(InstallProgress.NothingInstallable);
            return;
        }

        log?.Report(InstallProgress.Installing(packages!));

        var args = new List<string> { "apt-get", "install", "-y" };
        args.AddRange(packages!);

        var result = await ProcessRunner
            .RunToCompletionAsync(new ProcessSpec("pkexec", args), ct)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            log?.Report(InstallProgress.Completed);
            return;
        }

        // Teknisk engelsk for logg og feilsøking; brukerens formulering ligger i ressursene og
        // velges ut fra CommandFailure.
        var (failure, reason) = result.ExitCode switch
        {
            126 => (CommandFailure.PrivilegeDeclined, "the password dialog was dismissed"),
            127 => (CommandFailure.CommandNotFound, "pkexec was not found"),
            100 => (CommandFailure.PackageManagerFailed, "apt failed"),
            _ => (CommandFailure.Unknown,
                result.CombinedOutput.Trim() is { Length: > 0 } o ? o[^Math.Min(300, o.Length)..] : "unknown error"),
        };
        throw new PrivilegedCommandException(
            $"Installing {string.Join(", ", packages)} failed (exit {result.ExitCode}): {reason}",
            failure,
            result.ExitCode);
    }
}
