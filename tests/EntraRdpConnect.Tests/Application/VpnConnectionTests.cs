using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Tests.Fakes;
using Xunit;

namespace EntraRdpConnect.Tests.Application;

public sealed class VpnConnectionTests
{
    private static readonly VpnStatus Down = VpnStatus.Down;
    private static readonly VpnStatus Connected = new() { InterfaceUp = true, ReceivedBytes = 2048 };

    /// <summary>Synkron progress-samler (i motsetning til Progress&lt;T&gt; som poster asynkront).</summary>
    private sealed class PhaseLog : IProgress<VpnPhase>
    {
        public List<VpnPhase> Phases { get; } = [];
        public void Report(VpnPhase value) => Phases.Add(value);
    }

    [Fact]
    public async Task Kobler_til_naar_handshake_kommer_etter_noen_poll()
    {
        // initial=Down → up → poll: Down, Down, Connected
        var controller = new FakeVpnController(Down, Down, Down, Connected);
        var connection = new VpnConnection(controller, new FakeSystemClock(),
            handshakeTimeout: TimeSpan.FromSeconds(30), pollInterval: TimeSpan.FromSeconds(1));
        var log = new PhaseLog();

        var status = await connection.EnsureConnectedAsync(log);

        Assert.True(status.IsConnected);
        Assert.Equal(1, controller.BringUpCalls);
        Assert.Equal(VpnPhase.WaitingForHandshake, log.Phases[^2]);
        Assert.Equal(VpnPhase.Connected, log.Phases[^1]);
    }

    [Fact]
    public async Task Timer_ut_med_tydelig_feil_naar_push_uteblir()
    {
        var controller = new FakeVpnController(Down); // handshake kommer aldri
        var connection = new VpnConnection(controller, new FakeSystemClock(),
            handshakeTimeout: TimeSpan.FromSeconds(45), pollInterval: TimeSpan.FromSeconds(1));

        var ex = await Assert.ThrowsAsync<VpnHandshakeTimeoutException>(
            () => connection.EnsureConnectedAsync());

        // Unntaket bærer dataene presentasjonslaget trenger for å forklare hva som gikk galt —
        // selve teksten om push-varselet ligger i ressursene, ikke i kjernen.
        Assert.Equal(TimeSpan.FromSeconds(45), ex.Timeout);
        Assert.Equal(1, controller.BringUpCalls);
    }

    [Fact]
    public async Task Hopper_over_oppkobling_naar_tunnelen_alt_er_oppe()
    {
        var controller = new FakeVpnController(Connected);
        var connection = new VpnConnection(controller, new FakeSystemClock(),
            handshakeTimeout: TimeSpan.FromSeconds(45), pollInterval: TimeSpan.FromSeconds(1));
        var log = new PhaseLog();

        var status = await connection.EnsureConnectedAsync(log);

        Assert.True(status.IsConnected);
        Assert.Equal(0, controller.BringUpCalls);
        Assert.Equal([VpnPhase.AlreadyConnected], log.Phases);
    }
}
