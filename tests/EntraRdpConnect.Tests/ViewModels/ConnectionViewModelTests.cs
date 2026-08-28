using EntraRdpConnect.App.ViewModels;
using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Tests.Fakes;
using Xunit;

namespace EntraRdpConnect.Tests.ViewModels;

/// <summary>
/// The rules that decide which action the user is offered. Both of them replaced a real bug:
/// Connect used to stay pressable while a handshake was pending, and Disconnect used to show
/// whenever a VPN was configured — including when no tunnel had ever been raised.
/// </summary>
public sealed class ConnectionViewModelTests
{
    private static readonly RdpConnectionInfo Info =
        new("work-desktop", 3389, "you@example.com", ["/sec:aad"]);

    private static ConnectionViewModel ViewModel(bool managesVpn)
    {
        var vpn = managesVpn
            ? new VpnConnection(
                new FakeVpnController(VpnStatus.Down),
                new FakeSystemClock(),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(1))
            : null;

        var orchestrator = new ConnectionOrchestrator(
            vpn, new FakeRdpSession(exitCode: 0), Info, (u, _) => Task.FromResult(u));

        return new ConnectionViewModel(orchestrator, "work-desktop · via VPN");
    }

    [Fact]
    public void Koble_fra_er_skjult_naar_ingen_tunnel_er_reist()
    {
        // The app can manage a VPN but has not raised anything yet. Offering Disconnect here
        // would be a button with nothing to disconnect.
        var vm = ViewModel(managesVpn: true);

        Assert.True(vm.ManagesVpn);
        Assert.False(vm.TunnelIsUp);
        Assert.False(vm.CanBringTunnelDown);
    }

    [Fact]
    public void Koble_fra_krever_baade_en_reist_tunnel_og_at_ingenting_paagaar()
    {
        var vm = ViewModel(managesVpn: true);

        vm.TunnelIsUp = true;
        Assert.True(vm.CanBringTunnelDown);

        // It must not be there mid-attempt — that is exactly what left Connect and Disconnect
        // both sitting there during a connection in flight.
        vm.IsBusy = true;
        Assert.False(vm.CanBringTunnelDown);
    }

    [Fact]
    public void Uten_vpn_utelater_skinnen_tunnel_og_push_stegene()
    {
        // Without a VPN the connection goes straight to RDP. A four-step rail would promise
        // two steps that never happen.
        var vm = ViewModel(managesVpn: false);

        Assert.Equal(["StepDesktop", "StepSignIn"], vm.Steps.Select(s => s.LabelKey));
    }

    [Fact]
    public void Med_vpn_har_skinnen_alle_fire_stegene_i_rekkefoelge()
    {
        var vm = ViewModel(managesVpn: true);

        Assert.Equal(
            ["StepTunnel", "StepPhone", "StepDesktop", "StepSignIn"],
            vm.Steps.Select(s => s.LabelKey));
    }

    [Theory]
    [InlineData(ConnectionAction.Connect)]
    [InlineData(ConnectionAction.Retry)]
    [InlineData(ConnectionAction.Cancel)]
    public void Hver_handling_slaar_opp_en_ekte_tekst(ConnectionAction action)
    {
        // The label is built as "Action" + the enum name. Rename a value without touching the
        // resource file and the button reads "!ActionXyz!" rather than failing — so we assert
        // that the lookup landed, not what it says.
        var vm = ViewModel(managesVpn: true);

        vm.PrimaryAction = action;

        Assert.False(vm.ActionLabel.StartsWith('!'), $"Fant ingen tekst for {action}");
    }

    [Fact]
    public void Avbryt_er_ikke_den_primaere_handlingen()
    {
        var vm = ViewModel(managesVpn: true);

        vm.PrimaryAction = ConnectionAction.Cancel;
        Assert.False(vm.ActionIsPrimary);

        vm.PrimaryAction = ConnectionAction.Retry;
        Assert.True(vm.ActionIsPrimary);
    }
}
