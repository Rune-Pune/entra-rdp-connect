using System.Diagnostics;
using EntraRdpConnect.Infrastructure.Capture;

namespace EntraRdpConnect.Infrastructure.Browser;

/// <summary>
/// Logger inn via en DEDIKERT Firefox-instans (egen profil) som lukkes automatisk når koden er
/// fanget — vi dreper akkurat denne instansen, så vinduet/fanen forsvinner av seg selv.
///
/// Profilen gjenbrukes mellom økter: første innlogging krever MFA, men cookies lagres, så senere
/// oppkoblinger hopper som regel over MFA (SSO). Ren historikk gjør at tidsstempel-filteret alltid
/// plukker riktig, ferske kode.
/// </summary>
public sealed class DedicatedFirefoxLogin
{
    private readonly TimeSpan _timeout;
    private readonly string _profileDir;

    public DedicatedFirefoxLogin(TimeSpan timeout, string? profileDir = null)
    {
        _timeout = timeout;
        _profileDir = profileDir ?? DefaultProfileDir();
    }

    /// <summary>Resolver-signatur: åpner login-URL-en i en dedikert Firefox, fanger koden, lukker.</summary>
    public async Task<Uri> CaptureAsync(Uri loginUrl, CancellationToken ct)
    {
        BrowserProfile.Prepare(_profileDir);
        var since = DateTimeOffset.UtcNow;

        var firefox = Process.Start(new ProcessStartInfo
        {
            FileName = "firefox",
            ArgumentList = { "--no-remote", "--profile", _profileDir, "--new-window", loginUrl.ToString() },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Could not start Firefox.");

        // Drener Firefox' egen GTK-/snap-støy vekk fra terminalen vår (og hindrer at pipa fylles).
        firefox.OutputDataReceived += static (_, _) => { };
        firefox.ErrorDataReceived += static (_, _) => { };
        firefox.BeginOutputReadLine();
        firefox.BeginErrorReadLine();

        try
        {
            // Tett polling: den dedikerte profilens places.sqlite er bitteliten, og rask fangst
            // krymper glippet der Firefox rekker å vise /common/wrongplace før vi lukker den.
            var capture = new FirefoxPlacesCapture(
                Path.Combine(_profileDir, "places.sqlite"), TimeSpan.FromMilliseconds(200));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_timeout);
            return await capture.CaptureAsync(since, timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            TryKill(firefox);
            firefox.Dispose();

            // Filene nettleseren laget under økta har sine egne rettigheter (0644).
            BrowserProfile.Harden(_profileDir);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { /* rakk å avslutte selv */ }
    }

    private static string DefaultProfileDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Snap-Firefox er innelukket og når bare visse stier — legg profilen der snap-en har tilgang.
        var snapCommon = Path.Combine(home, "snap", "firefox", "common");
        var baseDir = Directory.Exists(snapCommon)
            ? snapCommon
            : Path.Combine(home, ".config", "entra-rdp-connect");

        return Path.Combine(baseDir, "firefox-login");
    }


}
