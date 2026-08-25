using EntraRdpConnect.Infrastructure.Diagnostics;
using Xunit;

namespace EntraRdpConnect.Tests.Diagnostics;

public sealed class DependencyCatalogTests
{
    [Theory]
    // Pakkenavn verifisert mot Ubuntu 24.04 — de to siste er lette å gjette feil.
    [InlineData("xfreerdp3", "freerdp3-x11")]
    [InlineData("sqlite3", "sqlite3")]
    [InlineData("dig", "bind9-dnsutils")]
    [InlineData("script", "bsdutils")]
    public void Kjente_verktoey_peker_paa_riktig_apt_pakke(string command, string expected)
    {
        var dep = DependencyChecker.Required.Single(d => d.Command == command);
        Assert.Equal(expected, dep.AptPackage);
    }

    [Theory]
    [InlineData("uwg-quick")]
    [InlineData("uwg")]
    public void UniFi_verktoeyene_er_markert_som_ikke_installerbare(string command)
    {
        // Følger med UniFi Identity og finnes ikke i noen pakkebrønn — appen må si fra
        // at brukeren må hente dem selv, ikke late som den kan installere dem.
        var dep = DependencyChecker.Required.Single(d => d.Command == command);
        Assert.Null(dep.AptPackage);
    }

    [Fact]
    public void Alle_paakrevde_verktoey_har_et_formaal_beskrevet()
    {
        foreach (var dep in DependencyChecker.Required)
            Assert.False(string.IsNullOrWhiteSpace(dep.Purpose), $"{dep.Command} mangler formål");
    }
}
