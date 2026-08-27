namespace EntraRdpConnect.Infrastructure.Diagnostics;

/// <param name="AptPackage">Pakken som gir kommandoen, eller null når den ikke finnes i
/// pakkebrønnen og må installeres manuelt (UniFi-klienten).</param>
public sealed record Dependency(string Command, string Purpose, bool Required, string? AptPackage = null);

public sealed record DependencyResult(Dependency Dependency, string? ResolvedPath)
{
    public bool Found => ResolvedPath is not null;
}

/// <summary>
/// Sjekker at binærene appen orkestrerer finnes på PATH. Appen reimplementerer ingenting selv,
/// så manglende systemavhengigheter må fanges tidlig med tydelig feil.
///
/// Pakkenavnene er verifisert mot Ubuntu 24.04 (merk: `dig` kommer fra bind9-dnsutils og
/// `script` fra bsdutils — ikke dnsutils/util-linux som man kunne tro).
/// </summary>
public static class DependencyChecker
{
    public static readonly IReadOnlyList<Dependency> Required =
    [
        new("xfreerdp3", "RDP client (FreeRDP 3)", Required: true, AptPackage: "freerdp3-x11"),
        new("uwg-quick", "VPN up/down (UniFi Adaptive VPN)", Required: true),
        new("uwg",       "VPN status", Required: true),
        new("pkexec",    "Graphical privilege escalation for the VPN", Required: true, AptPackage: "pkexec"),
        new("firefox",   "Browser for the Entra sign-in", Required: true, AptPackage: "firefox"),
        new("script",    "Pseudo-terminal for the xfreerdp3 AAD sign-in", Required: true, AptPackage: "bsdutils"),
        new("dig",       "DNS lookup against the gateway on DHCP changes", Required: false, AptPackage: "bind9-dnsutils"),
        new("sqlite3",   "Reading browser history (capturing the OAuth code)", Required: false, AptPackage: "sqlite3"),
    ];

    public static IReadOnlyList<DependencyResult> Check(IEnumerable<Dependency>? deps = null)
        => (deps ?? Required).Select(d => new DependencyResult(d, Resolve(d.Command))).ToList();

    /// <summary>Slår opp en kjørbar fil på PATH (som `which`), uten å shelle ut.</summary>
    public static string? Resolve(string command)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
            return null;

        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, command);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
