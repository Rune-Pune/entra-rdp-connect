using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Tests.Fakes;

internal sealed class FakeBrowserLauncher : IBrowserLauncher
{
    public List<Uri> Opened { get; } = [];

    public Task OpenAsync(Uri url, CancellationToken ct = default)
    {
        Opened.Add(url);
        return Task.CompletedTask;
    }
}
