namespace EntraRdpConnect.Core.Ports;

/// <summary>Port: tid og forsinkelser. Abstrahert så tilstandsmaskinens to reelle timeouts —
/// handshake-ventingen og OAuth-kodens &lt;60s-utløp — kan testes deterministisk med en fake klokke.</summary>
public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }

    Task DelayAsync(TimeSpan duration, CancellationToken ct = default);
}
