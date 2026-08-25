using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Tests.Fakes;

internal sealed class FakeRedirectUrlCapture : IRedirectUrlCapture
{
    private readonly Uri _result;

    public DateTimeOffset? SinceSeen { get; private set; }

    public FakeRedirectUrlCapture(Uri result) => _result = result;

    public Task<Uri> CaptureAsync(DateTimeOffset since, CancellationToken ct = default)
    {
        SinceSeen = since;
        return Task.FromResult(_result);
    }
}
