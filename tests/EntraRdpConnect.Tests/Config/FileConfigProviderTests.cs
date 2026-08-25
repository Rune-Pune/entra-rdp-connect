using EntraRdpConnect.Core.Configuration;
using EntraRdpConnect.Infrastructure.Config;
using Xunit;

namespace EntraRdpConnect.Tests.Config;

public sealed class FileConfigProviderTests
{
    [Fact]
    public async Task Leser_verdier_fra_fil_og_beholder_defaults_for_utelatte_felt()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, """
            {
              "rdp": { "host": "test-host", "hostIp": "10.0.0.1", "port": 3390, "user": "u@x.no" },
              "vpn": { "interface": "wgtest" }
            }
            """);
        try
        {
            var provider = new FileConfigProvider(path);

            Assert.True(provider.Exists);
            var cfg = await provider.GetAsync();

            Assert.Equal("test-host", cfg.Rdp.Host);
            Assert.Equal(3390, cfg.Rdp.Port);
            Assert.Equal("wgtest", cfg.Vpn.Interface);
            // Felt utelatt i JSON beholder default fra AppConfig:
            Assert.Equal(45, cfg.Vpn.HandshakeTimeoutSeconds);
            Assert.Contains("/sec:aad", cfg.Rdp.ExtraArgs);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Manglende_fil_gir_tydelig_FileNotFoundException()
    {
        var provider = new FileConfigProvider("/finnes/ikke/entra-rdp-connect/config.json");

        Assert.False(provider.Exists);
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => provider.GetAsync());
        Assert.Contains("config.sample.json", ex.Message);
    }

    [Fact]
    public async Task Lagring_oppretter_manglende_mappe_og_kan_leses_tilbake()
    {
        // Fersk bruker: ingen config finnes, verdier fylles inn i Innstillinger og lagres.
        var dir = Path.Combine(Path.GetTempPath(), "entra-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "entra-rdp-connect", "config.json");
        var provider = new FileConfigProvider(path);
        Assert.False(provider.Exists);

        var config = new AppConfig
        {
            Vpn = new VpnConfig { Interface = "wg0work", HandshakeTimeoutSeconds = 30 },
            Rdp = new RdpConfig { Host = "work-desktop", HostIp = "10.0.0.10", Port = 3389, User = "you@example.com" },
        };

        try
        {
            await provider.SaveAsync(config);

            Assert.True(provider.Exists);
            var reloaded = await provider.GetAsync();
            Assert.Equal("work-desktop", reloaded.Rdp.Host);
            Assert.Equal("you@example.com", reloaded.Rdp.User);
            Assert.Equal("wg0work", reloaded.Vpn.Interface);
            Assert.Equal(30, reloaded.Vpn.HandshakeTimeoutSeconds);
            // Argumentene må overleve tur-retur, ellers feiler Entra-innloggingen.
            Assert.Contains("/sec:aad", reloaded.Rdp.ExtraArgs);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Lagret_config_er_bare_lesbar_for_eier()
    {
        var dir = Path.Combine(Path.GetTempPath(), "entra-perm-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "entra-rdp-connect", "config.json");
        try
        {
            await new FileConfigProvider(path).SaveAsync(new AppConfig());

            // Filen inneholder UPN og interne vertsnavn — ingen andre brukere skal kunne lese den.
            var mode = File.GetUnixFileMode(path);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DefaultConfigPath_respekterer_XDG_CONFIG_HOME()
    {
        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "/tmp/xdgtest");
            Assert.Equal("/tmp/xdgtest/entra-rdp-connect/config.json", FileConfigProvider.DefaultConfigPath());
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
        }
    }
}
