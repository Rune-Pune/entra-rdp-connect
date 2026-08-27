namespace EntraRdpConnect.Core.Application;

/// <summary>
/// Basis for feil i VPN-flyten.
///
/// Meldingene her er tekniske og engelske, ment for logger og feilsøking. Teksten brukeren ser
/// formuleres i presentasjonslaget ut fra unntakstypen og dataene den bærer — kjernen skal ikke
/// kjenne brukerens språk.
/// </summary>
public class VpnException : Exception
{
    public VpnException(string message) : base(message) { }
}

/// <summary>Handshake kom ikke innen fristen — nesten alltid fordi push-varselet ikke ble godkjent.</summary>
public sealed class VpnHandshakeTimeoutException : VpnException
{
    public TimeSpan Timeout { get; }

    public VpnHandshakeTimeoutException(TimeSpan timeout)
        : base($"No WireGuard handshake within {timeout.TotalSeconds:0} seconds.")
        => Timeout = timeout;
}

/// <summary>En underliggende kommando (uwg-quick via pkexec) feilet.</summary>
public sealed class VpnCommandException : VpnException
{
    public int ExitCode { get; }

    /// <summary>Årsaken, når den er kjent nok til å forklares på brukerens språk.</summary>
    public CommandFailure Failure { get; }

    public VpnCommandException(string message, CommandFailure failure, int exitCode) : base(message)
    {
        Failure = failure;
        ExitCode = exitCode;
    }
}
