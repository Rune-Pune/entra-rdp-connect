using Microsoft.Extensions.DependencyInjection;
using EntraRdpConnect.Cli;
using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Core.Ports;
using EntraRdpConnect.Infrastructure.Browser;
using EntraRdpConnect.Infrastructure.Capture;
using EntraRdpConnect.Infrastructure.Diagnostics;
using EntraRdpConnect.Infrastructure.Processes;
using EntraRdpConnect.Infrastructure.Rdp;
using EntraRdpConnect.Infrastructure.Vpn;

// Kommando-dispatch.
return args switch
{
    ["proc"] => await ProcessDemoAsync(),
    ["vpn-status"] => await VpnStatusAsync(),
    ["vpn-up"] => await VpnUpAsync(),
    ["vpn-down"] => await VpnDownAsync(),
    ["rdp"] => await RdpAsync(),
    ["connect"] => await ConnectCommandAsync(),
    _ => await StatusAsync(),
};

// ---- standard: status (avhengigheter + config) ----
static async Task<int> StatusAsync()
{
    Console.WriteLine("entra-rdp-connect — status\n");

    using var services = CompositionRoot.Build();
    var configProvider = services.GetRequiredService<IConfigProvider>();

    // UniFi-verktøyene er bare påkrevd når appen faktisk styrer VPN-tunnelen.
    var managesVpn = true;
    if (configProvider.Exists)
    {
        var loaded = await configProvider.GetAsync();
        managesVpn = !string.IsNullOrWhiteSpace(loaded.Vpn.Interface);
    }
    string[] vpnOnly = ["uwg-quick", "uwg"];

    Console.WriteLine("Systemavhengigheter:");
    var results = DependencyChecker.Check();
    foreach (var r in results)
    {
        var required = r.Dependency.Required && (managesVpn || !vpnOnly.Contains(r.Dependency.Command));
        var mark = r.Found ? "✓" : required ? "✗" : "–";
        var where = r.Found
            ? r.ResolvedPath
            : required ? "MANGLER (påkrevd)" : "ikke funnet (valgfri)";
        Console.WriteLine($"  {mark} {r.Dependency.Command,-11} {where}");
        Console.WriteLine($"      {r.Dependency.Purpose}");
    }

    var missingRequired = results
        .Where(r => !r.Found && r.Dependency.Required)
        .Where(r => managesVpn || !vpnOnly.Contains(r.Dependency.Command))
        .ToList();

    Console.WriteLine();

    if (configProvider.Exists)
    {
        var cfg = await configProvider.GetAsync();
        Console.WriteLine($"Config lastet fra {configProvider.Location}:");
        Console.WriteLine($"  RDP-host:  {cfg.Rdp.Host} ({cfg.Rdp.HostIp}:{cfg.Rdp.Port})");
        Console.WriteLine($"  Bruker:    {cfg.Rdp.User}");
        Console.WriteLine($"  VPN-if:    {cfg.Vpn.Interface}");
        Console.WriteLine($"  RDP-args:  {string.Join(' ', cfg.Rdp.ExtraArgs)}");
    }
    else
    {
        var dir = Path.GetDirectoryName(configProvider.Location);
        Console.WriteLine($"Ingen config på {configProvider.Location}. Opprett den:");
        Console.WriteLine($"    mkdir -p {dir}");
        Console.WriteLine($"    cp config.sample.json {configProvider.Location}");
        Console.WriteLine("  (fyll deretter inn ekte verdier)");
    }

    Console.WriteLine();
    if (missingRequired.Count > 0)
    {
        Console.WriteLine($"⚠ {missingRequired.Count} påkrevd(e) avhengighet(er) mangler — appen fungerer ikke før de er på plass.");
        return 1;
    }

    Console.WriteLine("✓ Alle påkrevde avhengigheter er på plass.");
    return 0;
}

// ---- vpn-status: rot-fri statuslesning (trygg når som helst) ----
static async Task<int> VpnStatusAsync()
{
    using var services = CompositionRoot.Build();
    var config = await services.GetRequiredService<IConfigProvider>().GetAsync();

    if (string.IsNullOrWhiteSpace(config.Vpn.Interface))
    {
        Console.WriteLine("Ingen VPN konfigurert (vpn.interface er tom) — appen styrer ikke noen tunnel.");
        return 0;
    }
    var controller = new UwgVpnController(config.Vpn.Interface);

    var status = await controller.GetStatusAsync();
    Console.WriteLine($"Interface {config.Vpn.Interface}: {(status.InterfaceUp ? "oppe" : "nede")}");
    Console.WriteLine($"Mottatt:   {status.ReceivedBytes} B");
    Console.WriteLine(status.IsConnected ? "✓ Tilkoblet (handshake OK)" : "✗ Ikke tilkoblet");
    return 0;
}

// ---- vpn-up: ekte oppkobling (pkexec + push på mobil) ----
static async Task<int> VpnUpAsync()
{
    using var services = CompositionRoot.Build();
    var config = await services.GetRequiredService<IConfigProvider>().GetAsync();

    if (string.IsNullOrWhiteSpace(config.Vpn.Interface))
    {
        Console.WriteLine("Ingen VPN konfigurert (vpn.interface er tom) — appen styrer ikke noen tunnel.");
        return 0;
    }
    var clock = services.GetRequiredService<ISystemClock>();

    var controller = new UwgVpnController(config.Vpn.Interface);
    var connection = new VpnConnection(
        controller, clock,
        handshakeTimeout: TimeSpan.FromSeconds(config.Vpn.HandshakeTimeoutSeconds),
        pollInterval: TimeSpan.FromSeconds(1));

    var progress = new Progress<VpnPhase>(phase => Console.WriteLine("  " + DescribeVpnPhase(phase)));
    try
    {
        var status = await connection.EnsureConnectedAsync(progress);
        Console.WriteLine($"✓ Tunnelen er oppe (mottatt {status.ReceivedBytes} B).");
        return 0;
    }
    catch (VpnException ex)
    {
        Console.WriteLine($"✗ {ex.Message}");
        return 1;
    }
}

// ---- vpn-down ----
static async Task<int> VpnDownAsync()
{
    using var services = CompositionRoot.Build();
    var config = await services.GetRequiredService<IConfigProvider>().GetAsync();

    if (string.IsNullOrWhiteSpace(config.Vpn.Interface))
    {
        Console.WriteLine("Ingen VPN konfigurert (vpn.interface er tom) — appen styrer ikke noen tunnel.");
        return 0;
    }
    var controller = new UwgVpnController(config.Vpn.Interface);

    try
    {
        await controller.BringDownAsync();
        Console.WriteLine("Tunnelen er tatt ned.");
        return 0;
    }
    catch (VpnException ex)
    {
        Console.WriteLine($"✗ {ex.Message}");
        return 1;
    }
}

// ---- rdp: RDP-økt (forutsetter at VPN allerede er oppe hvis appen styrer den) ----
static async Task<int> RdpAsync()
{
    using var services = CompositionRoot.Build();
    var config = await services.GetRequiredService<IConfigProvider>().GetAsync();

    // Styrer appen VPN, må tunnelen være oppe først. Uten VPN antar vi at verten er nåbar.
    if (!string.IsNullOrWhiteSpace(config.Vpn.Interface))
    {
        var vpn = new UwgVpnController(config.Vpn.Interface);
        if (!(await vpn.GetStatusAsync()).IsConnected)
        {
            Console.WriteLine("✗ VPN-tunnelen er ikke oppe. Kjør 'vpn-up' først.");
            return 1;
        }
    }

    var info = new RdpConnectionInfo(config.Rdp.Host, config.Rdp.Port, config.Rdp.User, config.Rdp.ExtraArgs);
    var rdpDebug = Environment.GetEnvironmentVariable("ERC_RDP_DEBUG") == "1";
    IRdpSession session = new FreeRdpSession(echoRawLines: rdpDebug); // ERC_RDP_DEBUG=1 for rå output
    var progress = new Progress<RdpPhase>(phase => Console.WriteLine($"  [{phase}]"));

    var resolver = CreateLoginResolver(services.GetRequiredService<ISystemClock>());

    try
    {
        var exit = await session.ConnectAsync(info, resolver, progress);
        Console.WriteLine($"RDP-økt avsluttet (exit {exit}).");
        return exit;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ {ex.Message}");
        return 1;
    }
}

// ---- connect: hele kjeden — VPN opp (hvis nødvendig) + RDP med auto-fangst ----
static async Task<int> ConnectCommandAsync()
{
    using var services = CompositionRoot.Build();
    var config = await services.GetRequiredService<IConfigProvider>().GetAsync();
    var clock = services.GetRequiredService<ISystemClock>();

    // VPN er valgfritt — tomt interface betyr at vi går rett på RDP.
    var vpn = string.IsNullOrWhiteSpace(config.Vpn.Interface)
        ? null
        : new VpnConnection(
            new UwgVpnController(config.Vpn.Interface), clock,
            handshakeTimeout: TimeSpan.FromSeconds(config.Vpn.HandshakeTimeoutSeconds),
            pollInterval: TimeSpan.FromSeconds(1));

    var rdpInfo = new RdpConnectionInfo(config.Rdp.Host, config.Rdp.Port, config.Rdp.User, config.Rdp.ExtraArgs);
    var rdpDebug = Environment.GetEnvironmentVariable("ERC_RDP_DEBUG") == "1";
    var rdp = new FreeRdpSession(echoRawLines: rdpDebug);

    var orchestrator = new ConnectionOrchestrator(vpn, rdp, rdpInfo, CreateLoginResolver(clock));

    var vpnProgress = new Progress<VpnPhase>(p => Console.WriteLine("  [VPN] " + DescribeVpnPhase(p)));
    var rdpProgress = new Progress<RdpPhase>(p => Console.WriteLine($"  [RDP] {p}"));

    try
    {
        var exit = await orchestrator.ConnectAsync(vpnProgress, rdpProgress);
        Console.WriteLine($"Økt avsluttet (exit {exit}). Tunnelen står oppe — bruk 'vpn-down' for å koble ned.");
        return exit;
    }
    catch (VpnException ex)
    {
        Console.WriteLine($"✗ {ex.Message}");
        return 1;
    }
}

// Bygger login-resolveren: auto-fangst (Firefox) hvis tilgjengelig, ellers manuell paste. Aldri fatalt.
static Func<Uri, CancellationToken, Task<Uri>> CreateLoginResolver(ISystemClock clock)
{
    EntraLoginResolver? autoResolver = null;
    try
    {
        autoResolver = new EntraLoginResolver(
            new FirefoxLauncher(), new FirefoxPlacesCapture(), clock,
            captureTimeout: TimeSpan.FromSeconds(50));
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"(Auto-fangst utilgjengelig: {ex.Message} Bruker manuell innliming.)");
    }

    return async (loginUrl, ct) =>
    {
        if (autoResolver is not null)
        {
            Console.WriteLine();
            Console.WriteLine("Åpner Firefox — logg inn (MFA hoppes over hvis du alt er innlogget)…");
            try
            {
                var redirect = await autoResolver.ResolveAsync(loginUrl, ct);
                Console.WriteLine("✓ Fanget koden automatisk fra Firefox-historikken.");
                return redirect;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Console.WriteLine("Auto-fangst timet ut — faller tilbake til manuell innliming.");
            }
        }

        return ReadRedirectManually(loginUrl);
    };

    static Uri ReadRedirectManually(Uri loginUrl)
    {
        Console.WriteLine();
        Console.WriteLine("Åpne denne i nettleseren og logg inn:");
        Console.WriteLine($"  {loginUrl}");
        Console.WriteLine("Lim inn nativeclient?code=-URL-en her og trykk Enter (vær rask, <60 s):");
        var pasted = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(pasted) || !Uri.TryCreate(pasted, UriKind.Absolute, out var manual))
            throw new InvalidOperationException("Ugyldig eller tom URL limt inn.");
        return manual;
    }
}

// Menneskelige meldinger for VPN-fasene.
static string DescribeVpnPhase(VpnPhase phase) => phase switch
{
    VpnPhase.NotConfigured => "Ingen VPN konfigurert — går rett på fjernskrivebordet.",
    VpnPhase.AlreadyConnected => "Tunnelen er allerede oppe.",
    VpnPhase.BringingUp => "Reiser tunnelen (godkjenn pkexec-dialogen)…",
    VpnPhase.WaitingForHandshake => "Venter på handshake — godkjenn push i UniFi Identity-appen på mobilen…",
    VpnPhase.Connected => "Handshake OK.",
    VpnPhase.TimedOut => "Tidsavbrudd.",
    _ => phase.ToString(),
};

// ---- proc: ProcessRunner-demo ----
static async Task<int> ProcessDemoAsync()
{
    Console.WriteLine("ProcessRunner-demo\n");

    // [1] Kjør til slutt og samle output — mønsteret for `dig` og `uwg`-status.
    Console.WriteLine("[1] RunToCompletion: xfreerdp3 /version");
    var result = await ProcessRunner.RunToCompletionAsync(new ProcessSpec("xfreerdp3", "/version"));
    Console.WriteLine($"    exit={result.ExitCode}, stdout-linjer={result.Stdout.Count}");
    foreach (var line in result.Stdout)
        Console.WriteLine($"    | {line}");

    // [2] Interaktiv supervisjon + stdin — mønsteret for xfreerdp3-innloggingen:
    //     vi skriver koden til stdin; prosessen leser den når den er klar (buffret i pipen).
    Console.WriteLine("\n[2] Supervisert stdin/stdout (bash leser én linje og ekko-er den):");
    await using var runner = ProcessRunner.Start(
        new ProcessSpec("bash", "-c", "echo klar; read svar; echo \"du skrev: $svar\"") { RedirectStdin = true },
        onStdout: line => Console.WriteLine($"    stdout> {line}"),
        onStderr: line => Console.WriteLine($"    stderr> {line}"));

    await runner.WriteStdinLineAsync("heisann");
    runner.CloseStdin();
    var exit = await runner.WaitForExitAsync();
    Console.WriteLine($"    exit={exit}");

    return 0;
}
