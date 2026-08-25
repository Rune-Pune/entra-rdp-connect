using EntraRdpConnect.Infrastructure.Capture;
using Xunit;

namespace EntraRdpConnect.Tests.Capture;

public sealed class ChromiumHistoryCaptureTests
{
    [Fact]
    public void Tidsstempel_matcher_verdi_observert_i_chromium()
    {
        // Fasit hentet fra en ekte Chromium 151-historikk: besøket skjedde
        // 2026-08-22 20:22:19 UTC og ble lagret som 13431903739454625 (µs siden 1601).
        var observed = 13_431_903_739_454_625L;
        var visitTime = new DateTimeOffset(2026, 8, 22, 20, 22, 19, TimeSpan.Zero);

        var converted = ChromiumHistoryCapture.ToChromiumTime(visitTime);

        // Chromium lagrer mikrosekundpresisjon; vi sammenlikner på hele sekundet.
        Assert.Equal(observed / 1_000_000, converted / 1_000_000);
    }

    [Fact]
    public void Unix_epoken_er_11644473600_sekunder_etter_1601()
    {
        var atUnixEpoch = ChromiumHistoryCapture.ToChromiumTime(DateTimeOffset.UnixEpoch);
        Assert.Equal(11_644_473_600L * 1_000_000L, atUnixEpoch);
    }

    [Fact]
    public void Senere_tidspunkt_gir_hoeyere_tidsstempel()
    {
        var earlier = ChromiumHistoryCapture.ToChromiumTime(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var later = ChromiumHistoryCapture.ToChromiumTime(new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero));
        Assert.Equal(1_000_000L, later - earlier);
    }
}
