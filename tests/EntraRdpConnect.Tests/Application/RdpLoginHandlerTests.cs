using EntraRdpConnect.Core.Application;
using Xunit;

namespace EntraRdpConnect.Tests.Application;

public sealed class RdpLoginHandlerTests
{
    private const string RedirectUrl = "https://login.microsoftonline.com/common/oauth2/nativeclient?code=ABC123";

    [Fact]
    public async Task Detekterer_login_url_kaller_resolver_og_skriver_kode_til_stdin()
    {
        Uri? seenLoginUrl = null;
        var submitted = new List<string>();

        var handler = new RdpLoginHandler(
            resolveRedirectUrl: (url, ct) =>
            {
                seenLoginUrl = url;
                return Task.FromResult(new Uri(RedirectUrl));
            },
            submit: (text, ct) =>
            {
                submitted.Add(text);
                return Task.CompletedTask;
            });

        handler.OnLine("en irrelevant linje");
        handler.OnLine("[INFO] - Browse to: https://login.microsoftonline.com/common/oauth2/authorize?x=1");
        await handler.Completion;

        Assert.Equal("https://login.microsoftonline.com/common/oauth2/authorize?x=1", seenLoginUrl?.ToString());
        Assert.Single(submitted);
        Assert.Contains("nativeclient?code=ABC123", submitted[0]);
    }

    [Fact]
    public async Task Reagerer_bare_paa_foerste_browse_to_linje()
    {
        var resolverCalls = 0;

        var handler = new RdpLoginHandler(
            resolveRedirectUrl: (url, ct) =>
            {
                resolverCalls++;
                return Task.FromResult(new Uri(RedirectUrl));
            },
            submit: (text, ct) => Task.CompletedTask);

        handler.OnLine("Browse to: https://login.microsoftonline.com/a/authorize");
        handler.OnLine("Browse to: https://login.microsoftonline.com/b/authorize");
        await handler.Completion;

        Assert.Equal(1, resolverCalls);
    }
}
