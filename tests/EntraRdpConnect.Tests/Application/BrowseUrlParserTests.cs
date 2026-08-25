using EntraRdpConnect.Core.Application;
using Xunit;

namespace EntraRdpConnect.Tests.Application;

public sealed class BrowseUrlParserTests
{
    [Theory]
    [InlineData(
        "Browse to: https://login.microsoftonline.com/common/oauth2/authorize?client_id=abc",
        "https://login.microsoftonline.com/common/oauth2/authorize?client_id=abc")]
    [InlineData(
        "[12:00:00:000] [INFO][com.freerdp.core] - Browse to: https://login.microsoftonline.com/foo",
        "https://login.microsoftonline.com/foo")]
    public void Trekker_ut_login_url(string line, string expected)
    {
        Assert.True(BrowseUrlParser.TryParse(line, out var url));
        Assert.Equal(expected, url!.ToString());
    }

    [Theory]
    [InlineData("bare en vanlig loggelinje")]
    [InlineData("Browse to: (ingen url her)")]
    [InlineData("")]
    public void Ignorerer_linjer_uten_gyldig_url(string line)
    {
        Assert.False(BrowseUrlParser.TryParse(line, out _));
    }
}
