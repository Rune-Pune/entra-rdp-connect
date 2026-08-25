using System.Diagnostics;
using EntraRdpConnect.Infrastructure.Capture;
using EntraRdpConnect.Infrastructure.Diagnostics;

namespace EntraRdpConnect.Infrastructure.Browser;

/// <summary>
/// Innlogging via en dedikert Chromium-instans (Chrome, Chromium, Edge, Brave …) med egen profil,
/// som lukkes automatisk når koden er fanget. Motstykket til <see cref="DedicatedFirefoxLogin"/>.
/// </summary>
public sealed class DedicatedChromiumLogin
{
    /// <summary>Kjente Chromium-baserte nettlesere, i prioritert rekkefølge.</summary>
    private static readonly string[] Candidates =
        ["google-chrome", "google-chrome-stable", "chromium", "chromium-browser",
         "brave-browser", "microsoft-edge", "vivaldi"];

    private readonly string _browser;
    private readonly string _profileDir;
    private readonly TimeSpan _timeout;

    public DedicatedChromiumLogin(string browser, TimeSpan timeout, string? profileDir = null)
    {
        _browser = browser;
        _timeout = timeout;
        _profileDir = profileDir ?? DefaultProfileDir(browser);
    }

    /// <summary>Første Chromium-baserte nettleser som finnes på PATH, eller null.</summary>
    public static string? FindBrowser()
        => Candidates.FirstOrDefault(c => DependencyChecker.Resolve(c) is not null);

    public async Task<Uri> CaptureAsync(Uri loginUrl, CancellationToken ct)
    {
        BrowserProfile.Prepare(_profileDir);
        var since = DateTimeOffset.UtcNow;

        var browser = Process.Start(new ProcessStartInfo
        {
            FileName = _browser,
            ArgumentList =
            {
                $"--user-data-dir={_profileDir}",
                "--no-first-run",
                "--no-default-browser-check",
                "--new-window",
                loginUrl.ToString(),
            },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException($"Klarte ikke å starte {_browser}.");

        // Drener nettleserens egen støy vekk fra terminalen.
        browser.OutputDataReceived += static (_, _) => { };
        browser.ErrorDataReceived += static (_, _) => { };
        browser.BeginOutputReadLine();
        browser.BeginErrorReadLine();

        try
        {
            // Verifisert plassering: <profil>/Default/History
            var history = Path.Combine(_profileDir, "Default", "History");
            var capture = new ChromiumHistoryCapture(history, TimeSpan.FromMilliseconds(500));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_timeout);
            return await capture.CaptureAsync(since, timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            TryKill(browser);
            browser.Dispose();

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

    /// <summary>Snap-pakkede nettlesere når ikke skjulte mapper i hjemmeområdet, så profilen må
    /// ligge under ~/snap/&lt;navn&gt;/common når snap-en er i bruk.</summary>
    private static string DefaultProfileDir(string browser)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var snapCommon = Path.Combine(home, "snap", browser, "common");
        if (Directory.Exists(snapCommon))
            return Path.Combine(snapCommon, "entra-rdp-login");

        return Path.Combine(home, ".config", "entra-rdp-connect", $"{browser}-login");
    }


}
