using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Tests.Fakes;
using Xunit;

namespace EntraRdpConnect.Tests.Application;

public sealed class EntraLoginResolverTests
{
    [Fact]
    public async Task Aapner_browser_med_login_url_og_returnerer_fanget_kode()
    {
        var browser = new FakeBrowserLauncher();
        var captured = new Uri("https://login.microsoftonline.com/common/oauth2/nativeclient?code=XYZ");
        var capture = new FakeRedirectUrlCapture(captured);
        var clock = new FakeSystemClock(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        var resolver = new EntraLoginResolver(browser, capture, clock, TimeSpan.FromSeconds(50));
        var loginUrl = new Uri("https://login.microsoftonline.com/common/oauth2/authorize?x=1");

        var result = await resolver.ResolveAsync(loginUrl);

        Assert.Equal(captured, result);
        Assert.Equal([loginUrl], browser.Opened);
        // Ferskhets-referansen skal være tidspunktet vi åpnet nettleseren.
        Assert.Equal(clock.UtcNow, capture.SinceSeen);
    }
}
