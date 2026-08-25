using EntraRdpConnect.Infrastructure.Net;
using Xunit;

namespace EntraRdpConnect.Tests.Net;

public sealed class HostsFileResolutionTests
{
    [Fact]
    public void Bygger_gyldig_hosts_linje()
    {
        Assert.Equal("10.0.0.10\twork-desktop", HostsFileResolution.BuildEntry("10.0.0.10", "work-desktop"));
    }

    [Fact]
    public void Trimmer_mellomrom()
    {
        Assert.Equal("10.0.0.10\twork-desktop", HostsFileResolution.BuildEntry(" 10.0.0.10 ", " work-desktop "));
    }

    [Theory]
    [InlineData("ikke-en-ip", "work-desktop")]
    [InlineData("10.0.0.10", "")]
    // Linja havner i en shell-kommando som kjøres med rettighetshevning, så ingenting med
    // anførselstegn, semikolon eller mellomrom skal slippe gjennom.
    [InlineData("10.0.0.10", "vert' ; rm -rf /")]
    [InlineData("10.0.0.10", "vert med mellomrom")]
    [InlineData("10.0.0.10; echo x", "work-desktop")]
    public void Avviser_ugyldige_verdier(string ip, string host)
    {
        Assert.Throws<ArgumentException>(() => HostsFileResolution.BuildEntry(ip, host));
    }
}
