namespace EntraRdpConnect.Core.Application;

/// <summary>
/// Hvorfor en privilegert kommando feilet.
///
/// De to første er vanlige og forklarlige — brukeren lukket passord-dialogen, eller verktøyet
/// mangler. Resten er uventet, og da er det bare den rå utskriften å vise. Koden lar
/// presentasjonslaget formulere de forklarlige tilfellene på brukerens språk.
/// </summary>
public enum CommandFailure
{
    /// <summary>pkexec avsluttet med 126: dialogen ble avbrutt eller ikke godkjent.</summary>
    PrivilegeDeclined,

    /// <summary>pkexec avsluttet med 127: kommandoen ble ikke funnet.</summary>
    CommandNotFound,

    /// <summary>apt avsluttet med 100: pakkeindeksen er som regel utdatert.</summary>
    PackageManagerFailed,

    /// <summary>Noe annet gikk galt; se meldingen.</summary>
    Unknown,
}

/// <summary>
/// En kommando kjørt med pkexec feilet, utenfor VPN-flyten (skriving til /etc/hosts,
/// pakkeinstallasjon). VPN-flyten bruker <see cref="VpnCommandException"/>, som CLI-en
/// fanger som del av <see cref="VpnException"/>.
/// </summary>
public sealed class PrivilegedCommandException : Exception
{
    public CommandFailure Failure { get; }
    public int ExitCode { get; }

    public PrivilegedCommandException(string message, CommandFailure failure, int exitCode)
        : base(message)
    {
        Failure = failure;
        ExitCode = exitCode;
    }
}
