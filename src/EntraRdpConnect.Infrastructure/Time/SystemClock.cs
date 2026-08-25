using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Infrastructure.Time;

/// <summary>Ekte klokke-adapter for <see cref="ISystemClock"/>. I test byttes den ut med en
/// fake som lar oss styre tid og hoppe over ventingen.</summary>
public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public Task DelayAsync(TimeSpan duration, CancellationToken ct = default)
        => Task.Delay(duration, ct);
}
