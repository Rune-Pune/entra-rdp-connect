using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Tests.Fakes;

internal sealed class FakeRdpSession : IRdpSession
{
    private readonly int _exitCode;

    public int ConnectCalls { get; private set; }
    public RdpConnectionInfo? LastInfo { get; private set; }

    /// <summary>Kjøres ved ConnectAsync — brukes til å sjekke tilstand (f.eks. at VPN var oppe først).</summary>
    public Action? OnConnect { get; set; }

    public FakeRdpSession(int exitCode = 0) => _exitCode = exitCode;

    public Task<int> ConnectAsync(
        RdpConnectionInfo info,
        Func<Uri, CancellationToken, Task<Uri>> resolveRedirectUrl,
        IProgress<RdpPhase>? progress = null,
        CancellationToken ct = default)
    {
        ConnectCalls++;
        LastInfo = info;
        OnConnect?.Invoke();
        return Task.FromResult(_exitCode);
    }
}
