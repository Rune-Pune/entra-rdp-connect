using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Tests.Fakes;
using Xunit;

namespace EntraRdpConnect.Tests.Application;

public sealed class ConnectionOrchestratorTests
{
    private static readonly VpnStatus Down = VpnStatus.Down;
    private static readonly VpnStatus Connected = new() { InterfaceUp = true, ReceivedBytes = 1024 };

    private static VpnConnection Vpn(FakeVpnController controller) =>
        new(controller, new FakeSystemClock(), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));

    [Fact]
    public async Task Reiser_vpn_foer_rdp_og_returnerer_exit_kode()
    {
        var controller = new FakeVpnController(Down, Connected); // initial Down → up → connected
        var rdp = new FakeRdpSession(exitCode: 7);
        var vpnUpWhenRdpStarted = false;
        rdp.OnConnect = () => vpnUpWhenRdpStarted = controller.BringUpCalls == 1;

        var info = new RdpConnectionInfo("work-desktop", 3389, "you@example.com", ["/sec:aad"]);
        var orchestrator = new ConnectionOrchestrator(Vpn(controller), rdp, info, (u, _) => Task.FromResult(u));

        var exit = await orchestrator.ConnectAsync();

        Assert.Equal(7, exit);
        Assert.Equal(1, rdp.ConnectCalls);
        Assert.True(vpnUpWhenRdpStarted, "VPN skal være reist før RDP starter");
        Assert.Equal(info, rdp.LastInfo);
    }

    [Fact]
    public async Task Uten_vpn_gaar_den_rett_paa_rdp()
    {
        // Målmaskinen kan være nåbar direkte, eller gjennom en tunnel satt opp utenfor appen.
        var rdp = new FakeRdpSession(exitCode: 0);
        var phases = new List<VpnPhase>();
        var orchestrator = new ConnectionOrchestrator(
            vpn: null, rdp,
            new RdpConnectionInfo("work-desktop", 3389, "you@example.com", ["/sec:aad"]),
            (u, _) => Task.FromResult(u));

        Assert.False(orchestrator.ManagesVpn);

        await orchestrator.ConnectAsync(new SyncProgress<VpnPhase>(phases.Add));

        Assert.Equal(1, rdp.ConnectCalls);
        Assert.Equal([VpnPhase.NotConfigured], phases);
    }

    [Fact]
    public async Task Uten_vpn_er_nedkobling_en_ikke_operasjon()
    {
        var orchestrator = new ConnectionOrchestrator(
            vpn: null, new FakeRdpSession(),
            new RdpConnectionInfo("h", 3389, "u@x.no", []), (u, _) => Task.FromResult(u));

        await orchestrator.DisconnectAsync();   // skal ikke kaste
    }

    /// <summary>Progress&lt;T&gt; poster asynkront; her vil vi ha rapportene med en gang.</summary>
    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    [Fact]
    public async Task Hopper_over_vpn_oppkobling_naar_tunnelen_alt_er_oppe()
    {
        var controller = new FakeVpnController(Connected);
        var rdp = new FakeRdpSession();
        var orchestrator = new ConnectionOrchestrator(
            Vpn(controller), rdp,
            new RdpConnectionInfo("h", 3389, "u", []), (u, _) => Task.FromResult(u));

        await orchestrator.ConnectAsync();

        Assert.Equal(0, controller.BringUpCalls);
        Assert.Equal(1, rdp.ConnectCalls);
    }
}
