using EntraRdpConnect.Core.Application;
using Xunit;

namespace EntraRdpConnect.Tests.Application;

public sealed class RedirectUrlValidatorTests
{
    [Fact]
    public void Godtar_ekte_retur_url()
    {
        var text = "https://login.microsoftonline.com/common/oauth2/nativeclient?code=0.AXkA-abc123";
        Assert.True(RedirectUrlValidator.TryParse(text, out var url));
        Assert.Equal(text, url!.ToString());
    }

    [Fact]
    public void Tolererer_mellomrom_rundt_innlimt_tekst()
    {
        Assert.True(RedirectUrlValidator.TryParse(
            "  https://login.microsoftonline.com/common/oauth2/nativeclient?code=abc  ", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ikke en url")]
    // Uten code= er adressen ubrukelig — typisk hvis man kopierer /common/wrongplace-siden.
    [InlineData("https://login.microsoftonline.com/common/wrongplace")]
    // Feil skjema skal ikke slippe gjennom.
    [InlineData("file:///etc/passwd?code=abc")]
    public void Avviser_ugyldig_innliming(string? text)
    {
        Assert.False(RedirectUrlValidator.TryParse(text, out var url));
        Assert.Null(url);
    }
}
