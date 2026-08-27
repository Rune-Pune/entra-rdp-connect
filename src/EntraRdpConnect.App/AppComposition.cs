using EntraRdpConnect.App.ViewModels;
using EntraRdpConnect.App.Localization;
using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Configuration;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Infrastructure.Browser;
using EntraRdpConnect.Infrastructure.Config;
using EntraRdpConnect.Infrastructure.Diagnostics;
using EntraRdpConnect.Infrastructure.Net;
using EntraRdpConnect.Infrastructure.Rdp;
using EntraRdpConnect.Infrastructure.Time;
using EntraRdpConnect.Infrastructure.Vpn;

namespace EntraRdpConnect.App;

/// <summary>Composition root for GUI-et: kobler konkrete adaptere til porter og bygger ViewModel-treet.</summary>
internal static class AppComposition
{
    public static async Task<MainViewModel> BuildAsync()
    {
        var provider = new FileConfigProvider();
        var clock = new SystemClock();

        AppConfig config;
        try { config = await provider.GetAsync(); }
        catch { config = new AppConfig(); }

        Localizer.Instance.SetLanguage(config.Language);

        // VPN er valgfritt: uten interface går vi rett på RDP og antar at verten er nåbar.
        var managesVpn = !string.IsNullOrWhiteSpace(config.Vpn.Interface);
        var vpn = managesVpn
            ? new VpnConnection(
                new UwgVpnController(config.Vpn.Interface), clock,
                TimeSpan.FromSeconds(config.Vpn.HandshakeTimeoutSeconds), TimeSpan.FromSeconds(1))
            : null;

        // Samme feilsøkingsbryter som i CLI-en: skriver xfreerdp3 sin rå output til stderr.
        var rdpDebug = Environment.GetEnvironmentVariable("ERC_RDP_DEBUG") == "1";
        var rdp = new FreeRdpSession(echoRawLines: rdpDebug);
        var rdpInfo = new RdpConnectionInfo(config.Rdp.Host, config.Rdp.Port, config.Rdp.User, config.Rdp.ExtraArgs);

        // Innlogging: dedikert Firefox med automatisk kodefangst når Firefox finnes, ellers
        // (og hvis fangsten feiler) manuell innliming i standard nettleser.
        var manualPrompt = new ManualCodePrompt(new XdgOpenBrowserLauncher());
        var orchestrator = new ConnectionOrchestrator(vpn, rdp, rdpInfo, BuildResolver(manualPrompt));

        var target = string.IsNullOrWhiteSpace(config.Rdp.Host)
            ? Localizer.Instance["SubtitleNoHostConfigured"]
            : managesVpn ? Localizer.Instance.Format("SubtitleViaVpn", config.Rdp.Host) : config.Rdp.Host;

        // Sjekker systemverktøyene ved oppstart, og kan tilby å installere det som finnes i apt.
        return new MainViewModel(
            new ConnectionViewModel(orchestrator, target, new AptSystemDependencies(managesVpn), manualPrompt),
            new SettingsViewModel(provider, config, new HostsFileResolution()));
    }

    /// <summary>
    /// Auto-fangst krever Firefox (vi leser dens historikk). Finnes den ikke — eller feiler
    /// fangsten — åpnes innloggingen i standard nettleser og brukeren limer inn koden selv.
    /// </summary>
    private static Func<Uri, CancellationToken, Task<Uri>> BuildResolver(ManualCodePrompt manualPrompt)
    {
        // Firefox foretrekkes: den skriver historikken til disk nesten umiddelbart, mens
        // Chromium bruker ~10–15 sekunder — trangt når koden utløper på under ett minutt.
        Func<Uri, CancellationToken, Task<Uri>>? auto = null;

        if (DependencyChecker.Resolve("firefox") is not null)
        {
            var firefox = new DedicatedFirefoxLogin(TimeSpan.FromSeconds(120));
            auto = firefox.CaptureAsync;
        }
        else if (DedicatedChromiumLogin.FindBrowser() is { } chromium)
        {
            var login = new DedicatedChromiumLogin(chromium, TimeSpan.FromSeconds(120));
            auto = login.CaptureAsync;
        }

        if (auto is null)
            return manualPrompt.RequestAsync;   // ingen kjent nettleser — la brukeren lime inn

        return async (loginUrl, ct) =>
        {
            try
            {
                return await auto(loginUrl, ct);
            }
            catch
            {
                // Tidsavbrudd eller feil i auto-fangsten — la brukeren fullføre manuelt.
                return await manualPrompt.RequestAsync(loginUrl, ct);
            }
        };
    }
}
