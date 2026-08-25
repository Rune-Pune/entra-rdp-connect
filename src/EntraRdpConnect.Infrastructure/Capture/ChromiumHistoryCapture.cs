using EntraRdpConnect.Core.Ports;
using EntraRdpConnect.Infrastructure.Processes;

namespace EntraRdpConnect.Infrastructure.Capture;

/// <summary>
/// Fanger nativeclient?code=-URL-en fra en Chromium-basert nettleser (Chrome, Chromium, Edge,
/// Brave …) ved å polle historikkdatabasen.
///
/// Verifisert mot Chromium 151: databasen ligger i <c>&lt;profil&gt;/Default/History</c> (merk
/// undermappa), tabellen heter <c>urls</c>, og <c>last_visit_time</c> er mikrosekunder siden
/// 1601-01-01 — ikke unix-tid.
///
/// NB: Chromium skriver historikken til disk med noen sekunders forsinkelse (~10–15 s observert),
/// i motsetning til Firefox som flusher nesten umiddelbart. Kodens levetid er under ett minutt,
/// så marginen er trangere her.
/// </summary>
public sealed class ChromiumHistoryCapture : IRedirectUrlCapture
{
    /// <summary>Sekunder mellom 1601-01-01 og unix-epoken.</summary>
    private const long EpochOffsetSeconds = 11_644_473_600L;

    private readonly string _historyPath;
    private readonly TimeSpan _pollInterval;

    public ChromiumHistoryCapture(string historyPath, TimeSpan? pollInterval = null)
    {
        _historyPath = historyPath;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
    }

    /// <summary>Konverterer et tidspunkt til Chromiums tidsstempel (µs siden 1601).</summary>
    internal static long ToChromiumTime(DateTimeOffset time)
        => (time.ToUnixTimeMilliseconds() + EpochOffsetSeconds * 1000L) * 1000L;

    public async Task<Uri> CaptureAsync(DateTimeOffset since, CancellationToken ct = default)
    {
        var sinceChromium = ToChromiumTime(since);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var url = await QueryLatestAsync(sinceChromium, ct).ConfigureAwait(false);
            if (url is not null)
                return url;

            await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
        }
    }

    private async Task<Uri?> QueryLatestAsync(long sinceChromium, CancellationToken ct)
    {
        if (!File.Exists(_historyPath))
            return null;

        var tempDir = Directory.CreateTempSubdirectory("entra-chromium");
        try
        {
            // Nettleseren holder databasen åpen — vi spør en kopi, sammen med journalen.
            // Treffer vi et øyeblikk der den skriver, feiler kopieringen; da venter vi til
            // neste poll i stedet for å avbryte hele innloggingen.
            var copy = Path.Combine(tempDir.FullName, "History");
            try
            {
                CopyIfExists(_historyPath, copy);
                CopyIfExists(_historyPath + "-journal", copy + "-journal");
                CopyIfExists(_historyPath + "-wal", copy + "-wal");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            var sql =
                "SELECT url FROM urls " +
                "WHERE url LIKE '%nativeclient?code=%' " +
                $"AND last_visit_time > {sinceChromium} " +
                "ORDER BY last_visit_time DESC LIMIT 1;";

            var result = await ProcessRunner
                .RunToCompletionAsync(new ProcessSpec("sqlite3", copy, sql), ct)
                .ConfigureAwait(false);

            if (!result.Succeeded)
                return null;   // torn kopi — prøv igjen ved neste poll

            var line = result.Stdout.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
            return !string.IsNullOrEmpty(line) && Uri.TryCreate(line, UriKind.Absolute, out var url)
                ? url
                : null;
        }
        finally
        {
            try { tempDir.Delete(recursive: true); }
            catch { /* best effort */ }
        }
    }

    private static void CopyIfExists(string src, string dst)
    {
        if (File.Exists(src))
            File.Copy(src, dst, overwrite: true);
    }
}
