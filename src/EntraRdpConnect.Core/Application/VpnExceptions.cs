namespace EntraRdpConnect.Core.Application;

/// <summary>Basis for feil i VPN-flyten.</summary>
public class VpnException : Exception
{
    public VpnException(string message) : base(message) { }
}

/// <summary>Handshake kom ikke innen fristen — nesten alltid fordi push-varselet ikke ble godkjent.</summary>
public sealed class VpnHandshakeTimeoutException : VpnException
{
    public TimeSpan Timeout { get; }

    public VpnHandshakeTimeoutException(TimeSpan timeout)
        : base($"Fikk ikke handshake innen {timeout.TotalSeconds:0} sekunder. " +
               "Ble push-varselet i UniFi Identity-appen på mobilen godkjent? Prøv igjen med mobilen klar.")
        => Timeout = timeout;
}

/// <summary>En underliggende kommando (uwg-quick via pkexec) feilet.</summary>
public sealed class VpnCommandException : VpnException
{
    public int ExitCode { get; }

    public VpnCommandException(string message, int exitCode) : base(message)
        => ExitCode = exitCode;
}
