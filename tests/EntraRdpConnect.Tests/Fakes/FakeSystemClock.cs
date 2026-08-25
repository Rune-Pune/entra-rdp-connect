using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Tests.Fakes;

/// <summary>Fake klokke: <see cref="DelayAsync"/> hopper fram i tid i stedet for å vente,
/// så timeout-tester kjører på mikrosekunder i stedet for 45 ekte sekunder.</summary>
internal sealed class FakeSystemClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; private set; }

    public FakeSystemClock(DateTimeOffset? start = null)
        => UtcNow = start ?? DateTimeOffset.UnixEpoch;

    public Task DelayAsync(TimeSpan duration, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        UtcNow += duration;
        return Task.CompletedTask;
    }
}
