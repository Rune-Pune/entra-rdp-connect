using EntraRdpConnect.Infrastructure.Rdp;
using Xunit;

namespace EntraRdpConnect.Tests.Rdp;

public sealed class FreeRdpSessionTests
{
    [Fact]
    public void Autorisasjonskoden_maskeres_i_feilsoekingsutskrift()
    {
        // Pseudo-terminalen ekko-er stdin tilbake på stdout, så koden dukker opp i outputen.
        var line = "https://login.microsoftonline.com/common/oauth2/nativeclient?code=0.AXkAhemmelig123";

        var masked = FreeRdpSession.MaskAuthorizationCode(line);

        Assert.DoesNotContain("hemmelig123", masked);
        Assert.Contains("code=***", masked);
    }

    [Fact]
    public void Maskeringen_stopper_ved_neste_parameter()
    {
        var masked = FreeRdpSession.MaskAuthorizationCode("...?code=abc123&state=xyz");
        Assert.Equal("...?code=***&state=xyz", masked);
    }

    [Fact]
    public void Vanlige_linjer_er_uendret()
    {
        const string line = "[INFO][com.freerdp.core] - Connected to host";
        Assert.Equal(line, FreeRdpSession.MaskAuthorizationCode(line));
    }
}
