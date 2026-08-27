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

    private static SettingsError[] ErrorsIn(SettingsDraft draft)
        => SettingsValidator.Validate(draft).Select(i => i.Error).ToArray();

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
    public void Tomt_vpn_interface_er_gyldig()
    {
        // VPN er valgfritt: uten interface styrer ikke appen tunnelen, og går rett på RDP.
        // Nyttig for dem som når maskinen direkte, eller bruker en annen VPN-klient.
        Assert.Empty(SettingsValidator.Validate(Valid() with { VpnInterface = "" }));
    }

    [Fact]
    public void Ip_som_vertsnavn_avvises()
    {
        // Den klassiske fellen: med IP kapper FreeRDP ved første punktum → AADSTS293004.
        Assert.Contains(SettingsError.HostMustNotBeIpAddress, ErrorsIn(Valid() with { Host = "10.0.0.10" }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("-1")]
    public void Ugyldig_port_avvises(string port)
    {
        Assert.Contains(SettingsError.PortOutOfRange, ErrorsIn(Valid() with { Port = port }));
    }

    [Theory]
    [InlineData("", SettingsError.UserRequired)]
    [InlineData("bareBrukernavn", SettingsError.UserNotUpn)]
    public void Ugyldig_bruker_avvises(string user, SettingsError expected)
    {
        Assert.Contains(expected, ErrorsIn(Valid() with { User = user }));
    }

    [Theory]
    [InlineData("dette-navnet-er-altfor-langt", SettingsError.VpnInterfaceTooLong)]
    [InlineData("wg0 work", SettingsError.VpnInterfaceInvalidCharacters)]
    public void Ugyldig_vpn_interface_avvises(string iface, SettingsError expected)
    {
        Assert.Contains(expected, ErrorsIn(Valid() with { VpnInterface = iface }));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("9999")]
    [InlineData("tull")]
    public void Urimelig_timeout_avvises(string timeout)
    {
        Assert.Contains(SettingsError.HandshakeTimeoutOutOfRange, ErrorsIn(Valid() with { HandshakeTimeout = timeout }));
    }

    [Fact]
    public void Argumenter_uten_sec_aad_avvises()
    {
        // Uten /sec:aad kommer man aldri gjennom Entra-innloggingen.
        Assert.Contains(SettingsError.ExtraArgsMissingAadFlag,
            ErrorsIn(Valid() with { ExtraArgs = "/cert:ignore +clipboard" }));
    }

    [Fact]
    public void Ugyldig_ip_baerer_verdien_som_utloeste_feilen()
    {
        // Presentasjonslaget skal kunne si hvilken verdi som var gal, uten at kjernen formulerer det.
        var issue = Assert.Single(SettingsValidator.Validate(Valid() with { HostIp = "ikke-en-ip" }));
        Assert.Equal(SettingsError.HostIpInvalid, issue.Error);
        Assert.Equal("ikke-en-ip", issue.Value);
    }

    [Fact]
    public void Flere_feil_rapporteres_samtidig()
    {
        var errors = ErrorsIn(new SettingsDraft("", "ugyldig-ip", "abc", "", "", "0", ""));
        Assert.True(errors.Length >= 6, $"forventet minst 6 feil, fikk {errors.Length}");
    }
}
