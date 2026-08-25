using EntraRdpConnect.Infrastructure.Capture;
using Xunit;

namespace EntraRdpConnect.Tests.Capture;

public sealed class FirefoxProfileLocatorTests
{
    [Fact]
    public void Finner_profil_med_Default_1()
    {
        var ini = """
            [Profile0]
            Name=default
            IsRelative=1
            Path=ccmp0oiw.default
            Default=1

            [General]
            StartWithLastProfile=1
            Version=2
            """;

        Assert.Equal("ccmp0oiw.default", FirefoxProfileLocator.ParseDefaultProfilePath(ini));
    }

    [Fact]
    public void Foretrekker_Install_seksjonens_Default()
    {
        var ini = """
            [Install4F96D1932A9F858E]
            Default=abcd1234.dev-edition-default
            Locked=1

            [Profile1]
            Name=default
            IsRelative=1
            Path=zzzz0000.default
            Default=1

            [Profile0]
            Name=dev
            IsRelative=1
            Path=abcd1234.dev-edition-default
            """;

        Assert.Equal("abcd1234.dev-edition-default", FirefoxProfileLocator.ParseDefaultProfilePath(ini));
    }

    [Fact]
    public void Faller_tilbake_til_foerste_profil_uten_default()
    {
        var ini = """
            [Profile0]
            Name=default
            Path=only0000.default
            """;

        Assert.Equal("only0000.default", FirefoxProfileLocator.ParseDefaultProfilePath(ini));
    }

    [Fact]
    public void Returnerer_null_uten_profiler()
    {
        Assert.Null(FirefoxProfileLocator.ParseDefaultProfilePath("[General]\nVersion=2\n"));
    }
}
