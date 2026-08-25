using EntraRdpConnect.Infrastructure.Processes;
using Xunit;

namespace EntraRdpConnect.Tests.Processes;

public sealed class ShellQuoteTests
{
    [Fact]
    public void Vanlige_argumenter_siteres()
    {
        Assert.Equal("'/v:work-desktop:3389'", ShellQuote.Single("/v:work-desktop:3389"));
    }

    [Theory]
    // Konfigverdier havner i en shell-kommando (script -c). Ingenting av dette skal kunne
    // bryte ut av sitatet og bli kjørt som en egen kommando.
    [InlineData("x; rm -rf ~")]
    [InlineData("x && curl evil.example")]
    [InlineData("x $(whoami)")]
    [InlineData("x `id`")]
    [InlineData("x | tee /tmp/x")]
    [InlineData("x\nrm -rf ~")]
    public void Metategn_mister_betydning(string dangerous)
    {
        var quoted = ShellQuote.Single(dangerous);

        Assert.StartsWith("'", quoted);
        Assert.EndsWith("'", quoted);
        // Innholdet mellom fnuttene inneholder ingen avsluttende fnutt som kan bryte ut.
        var inner = quoted[1..^1];
        Assert.DoesNotContain("'", inner.Replace("'\\''", ""));
    }

    [Fact]
    public void Fnutt_i_verdien_haandteres()
    {
        // Klassisk utbruddsforsøk: avslutt sitatet og legg på en kommando.
        var quoted = ShellQuote.Single("vert' ; rm -rf ~ ; echo '");
        Assert.Equal("'vert'\\'' ; rm -rf ~ ; echo '\\'''", quoted);
    }

    [Fact]
    public void Join_siterer_hvert_ledd_for_seg()
    {
        var line = ShellQuote.Join(["xfreerdp3", "/v:host:3389", "/u:you@example.com"]);
        Assert.Equal("'xfreerdp3' '/v:host:3389' '/u:you@example.com'", line);
    }
}
