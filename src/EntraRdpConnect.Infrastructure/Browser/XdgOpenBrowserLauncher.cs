using System.Diagnostics;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Infrastructure.Browser;

/// <summary>
/// Åpner en URL i brukerens standard nettleser via xdg-open — uavhengig av hvilken nettleser det er.
/// Brukes når automatisk kodefangst ikke er mulig, og brukeren limer inn koden manuelt i stedet.
/// </summary>
public sealed class XdgOpenBrowserLauncher : IBrowserLauncher
{
    public Task OpenAsync(Uri url, CancellationToken ct = default)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "xdg-open",
            ArgumentList = { url.ToString() },
            UseShellExecute = false,
        });
        return Task.CompletedTask;
    }
}
