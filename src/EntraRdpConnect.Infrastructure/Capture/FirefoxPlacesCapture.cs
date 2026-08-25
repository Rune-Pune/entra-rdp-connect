using EntraRdpConnect.Core.Ports;
using EntraRdpConnect.Infrastructure.Processes;

namespace EntraRdpConnect.Infrastructure.Capture;

/// <summary>
/// Fanger nativeclient?code=-URL-en ved å polle Firefox sin places.sqlite via sqlite3-CLI-en.
///
/// Firefox kjører databasen i WAL-modus mens den er åpen, så vi kopierer places.sqlite (+ -wal/-shm)
/// til en temp-mappe og spør kopien — da slipper vi lås-problemer og ser de nyeste skrivingene.
/// Tidsstempel-filteret (last_visit_date &gt; since) sikrer at vi bare tar en FERSK kode.
/// </summary>
public sealed class FirefoxPlacesCapture : IRedirectUrlCapture
{
    private readonly string _placesPath;
    private readonly TimeSpan _pollInterval;

    public FirefoxPlacesCapture(string? placesPath = null, TimeSpan? pollInterval = null)
    {
        _placesPath = placesPath
            ?? FirefoxProfileLocator.FindPlacesDatabasePath()
            ?? throw new InvalidOperationException(
                "Fant ikke Firefox places.sqlite. Er Firefox installert med en profil?");
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(750);
    }

    public async Task<Uri> CaptureAsync(DateTimeOffset since, CancellationToken ct = default)
    {
        // Firefox lagrer tidsstempler som mikrosekunder siden epoch (PRTime).
        var sinceMicros = since.ToUnixTimeMilliseconds() * 1000L;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var url = await QueryLatestAsync(sinceMicros, ct).ConfigureAwait(false);
            if (url is not null)
                return url;

            await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
        }
    }

    private async Task<Uri?> QueryLatestAsync(long sinceMicros, CancellationToken ct)
    {
        var tempDir = Directory.CreateTempSubdirectory("entra-places");
        try
        {
            // Treffer vi et øyeblikk der Firefox skriver, feiler kopieringen; da venter vi til
            // neste poll i stedet for å avbryte hele innloggingen.
            var copy = Path.Combine(tempDir.FullName, "places.sqlite");
            try
            {
                CopyIfExists(_placesPath, copy);
                CopyIfExists(_placesPath + "-wal", copy + "-wal");
                CopyIfExists(_placesPath + "-shm", copy + "-shm");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            var sql =
                "SELECT url FROM moz_places " +
                "WHERE url LIKE '%nativeclient?code=%' " +
                $"AND last_visit_date > {sinceMicros} " +
                "ORDER BY last_visit_date DESC LIMIT 1;";

            var result = await ProcessRunner
                .RunToCompletionAsync(new ProcessSpec("sqlite3", copy, sql), ct)
                .ConfigureAwait(false);

            // En torn kopi kan gi feil enkelte poll — behandle som «ingen treff» og prøv igjen.
            if (!result.Succeeded)
                return null;

            var line = result.Stdout.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
            return !string.IsNullOrEmpty(line) && Uri.TryCreate(line, UriKind.Absolute, out var url)
                ? url
                : null;
        }
        finally
        {
            try { tempDir.Delete(recursive: true); }
            catch { /* best effort opprydding */ }
        }
    }

    private static void CopyIfExists(string src, string dst)
    {
        if (File.Exists(src))
            File.Copy(src, dst, overwrite: true);
    }
}
