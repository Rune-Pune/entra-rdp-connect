using System.Diagnostics;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Infrastructure.Browser;

/// <summary>
/// Åpner en URL i Firefox. Kjører `firefox &lt;url&gt;` som fire-and-forget: åpner en fane i en
/// kjørende instans (standard-profil) og returnerer. Vi disposer Process-objektet uten å drepe
/// prosessen (Dispose terminerer den ikke).
/// </summary>
public sealed class FirefoxLauncher : IBrowserLauncher
{
    public Task OpenAsync(Uri url, CancellationToken ct = default)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "firefox",
            ArgumentList = { url.ToString() },
            UseShellExecute = false,
        });
        return Task.CompletedTask;
    }
}
