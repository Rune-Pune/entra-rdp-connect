using EntraRdpConnect.Core.Application;
using Xunit;

namespace EntraRdpConnect.Tests.Application;

public sealed class SettingsValidatorTests
{
    private static SettingsDraft Valid() => new(
        Host: "work-desktop",
        HostIp: "10.0.0.10",
        Port: "3389",
        User: "you@example.com",
        VpnInterface: "wg0work",
        HandshakeTimeout: "45",
        ExtraArgs: "/sec:aad /cert:ignore +clipboard");

    [Fact]
    public void Gyldige_innstillinger_gir_ingen_feil()
    {
        Assert.Empty(SettingsValidator.Validate(Valid()));
    }

    [Fact]
    public void Tom_ip_er_greit_den_er_valgfri()
    {
        Assert.Empty(SettingsValidator.Validate(Valid() with { HostIp = "" }));
    }

    [Fact]
    public void Ip_som_vertsnavn_avvises_med_forklaring()
    {
        // Den klassiske fellen: med IP kapper FreeRDP ved første punktum → AADSTS293004.
        var errors = SettingsValidator.Validate(Valid() with { Host = "10.0.0.10" });
        Assert.Contains(errors, e => e.Contains("NAVN") && e.Contains("IP"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("-1")]
    public void Ugyldig_port_avvises(string port)
    {
        Assert.Contains(SettingsValidator.Validate(Valid() with { Port = port }),
            e => e.Contains("Port"));
    }

    [Theory]
    [InlineData("", "må fylles ut")]
    [InlineData("bareBrukernavn", "UPN")]
    public void Ugyldig_bruker_avvises(string user, string expected)
    {
        Assert.Contains(SettingsValidator.Validate(Valid() with { User = user }),
            e => e.Contains(expected));
    }

    [Theory]
    [InlineData("dette-navnet-er-altfor-langt")]  // over 15 tegn
    [InlineData("wg0 work")]             // mellomrom
    public void Ugyldig_vpn_interface_avvises(string iface)
    {
        Assert.NotEmpty(SettingsValidator.Validate(Valid() with { VpnInterface = iface }));
    }

    [Fact]
    public void Tomt_vpn_interface_er_gyldig()
    {
        // VPN er valgfritt: uten interface styrer ikke appen tunnelen, og går rett på RDP.
        // Nyttig for dem som når maskinen direkte, eller bruker en annen VPN-klient.
        Assert.Empty(SettingsValidator.Validate(Valid() with { VpnInterface = "" }));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("9999")]
    [InlineData("tull")]
    public void Urimelig_timeout_avvises(string timeout)
    {
        Assert.Contains(SettingsValidator.Validate(Valid() with { HandshakeTimeout = timeout }),
            e => e.Contains("Handshake-timeout"));
    }

    [Fact]
    public void Argumenter_uten_sec_aad_avvises()
    {
        // Uten /sec:aad kommer man aldri gjennom Entra-innloggingen.
        Assert.Contains(SettingsValidator.Validate(Valid() with { ExtraArgs = "/cert:ignore +clipboard" }),
            e => e.Contains("/sec:aad"));
    }

    [Fact]
    public void Flere_feil_rapporteres_samtidig()
    {
        var errors = SettingsValidator.Validate(new SettingsDraft("", "ugyldig-ip", "abc", "", "", "0", ""));
        Assert.True(errors.Count >= 6, $"forventet minst 6 feil, fikk {errors.Count}");
    }
}
