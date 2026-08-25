using EntraRdpConnect.Infrastructure.Browser;
using Xunit;

namespace EntraRdpConnect.Tests.Browser;

public sealed class BrowserProfileTests
{
    /// <summary>Profilen inneholder en levende Entra-sesjon (cookies) og fangede code=-adresser.
    /// Andre brukere på maskinen skal ikke kunne lese den.</summary>
    [Fact]
    public void Prepare_gir_profilmappe_som_bare_eier_kan_lese()
    {
        var dir = NewTempPath();
        try
        {
            BrowserProfile.Prepare(dir);

            Assert.True(Directory.Exists(dir));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(dir));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Harden_strammer_filer_nettleseren_har_lagt_igjen()
    {
        // Firefox og Chromium lager sine egne filer med sine egne rettigheter (ofte 0644),
        // så innstrammingen må gjelde eksisterende innhold — ikke bare selve mappa.
        var dir = NewTempPath();
        try
        {
            Directory.CreateDirectory(dir);
            var cookies = Path.Combine(dir, "cookies.sqlite");
            File.WriteAllText(cookies, "late som om dette er en sesjon");
            File.SetUnixFileMode(cookies,
                UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupRead | UnixFileMode.OtherRead);   // 0644, slik nettleseren gjør

            var nested = Path.Combine(dir, "storage");
            Directory.CreateDirectory(nested);
            var nestedFile = Path.Combine(nested, "data.sqlite");
            File.WriteAllText(nestedFile, "x");
            File.SetUnixFileMode(nestedFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);

            BrowserProfile.Harden(dir);

            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(cookies));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(nestedFile));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(nested));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Harden_hopper_over_cache_men_tar_hemmelighetene()
    {
        // Chromium-cache kan bli tusenvis av filer. Den inneholder ingen sesjon, så vi lar den
        // være — ventetid rett foran et innloggingsvindu som utløper på under ett minutt er verre.
        var dir = NewTempPath();
        try
        {
            var cache = Path.Combine(dir, "Cache");
            Directory.CreateDirectory(cache);
            var cached = Path.Combine(cache, "blob_1");
            File.WriteAllText(cached, "mellomlagret nettside");
            File.SetUnixFileMode(cached, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);

            var cookies = Path.Combine(dir, "cookies.sqlite");
            File.WriteAllText(cookies, "sesjon");
            File.SetUnixFileMode(cookies, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);

            BrowserProfile.Harden(dir);

            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(cookies));
            // Selve cache-mappa strammes, men innholdet røres ikke.
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(cache));
            Assert.True(File.GetUnixFileMode(cached).HasFlag(UnixFileMode.OtherRead),
                "cache-innhold skal hoppes over");
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public void Harden_paa_mappe_som_ikke_finnes_kaster_ikke()
    {
        BrowserProfile.Harden(Path.Combine(Path.GetTempPath(), "finnes-ikke-" + Guid.NewGuid()));
    }

    private static string NewTempPath()
        => Path.Combine(Path.GetTempPath(), "entra-profile-" + Guid.NewGuid().ToString("N"));

    private static void Cleanup(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
