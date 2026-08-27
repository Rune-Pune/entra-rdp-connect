using System.Net;

namespace EntraRdpConnect.Core.Application;

/// <summary>Innstillingene slik de står i skjemaet — rå tekst, før de tolkes.</summary>
public sealed record SettingsDraft(
    string Host,
    string HostIp,
    string Port,
    string User,
    string VpnInterface,
    string HandshakeTimeout,
    string ExtraArgs);

/// <summary>Hva som er galt med et felt. Kjernen navngir problemet; presentasjonslaget
/// formulerer det på brukerens språk.</summary>
public enum SettingsError
{
    HostRequired,
    HostMustNotBeIpAddress,
    HostInvalidCharacters,
    HostIpInvalid,
    PortOutOfRange,
    UserRequired,
    UserNotUpn,
    VpnInterfaceTooLong,
    VpnInterfaceInvalidCharacters,
    HandshakeTimeoutOutOfRange,
    ExtraArgsEmpty,
    ExtraArgsMissingAadFlag,
}

/// <summary>Ett problem, med verdien som utløste det når den er verdt å vise.</summary>
public sealed record ValidationIssue(SettingsError Error, string? Value = null);

/// <summary>
/// Validerer innstillingene før de lagres. Reglene er hentet fra feil vi faktisk har møtt —
/// særlig at verten må oppgis som NAVN: skriver man en IP, kapper FreeRDP den ved første punktum
/// og Entra svarer AADSTS293004, en feilmelding det er nær umulig å gjette seg fram fra.
///
/// Returnerer koder, ikke tekst: kjernen skal verken kjenne språk eller ordlyd.
/// Ren funksjon uten I/O, så reglene kan enhetstestes.
/// </summary>
public static class SettingsValidator
{
    /// <summary>Grensen for nettverksnavn i Linux (IFNAMSIZ minus null-terminatoren).</summary>
    private const int MaxInterfaceNameLength = 15;

    public static IReadOnlyList<ValidationIssue> Validate(SettingsDraft draft)
    {
        var issues = new List<ValidationIssue>();

        var host = draft.Host.Trim();
        if (host.Length == 0)
            issues.Add(new(SettingsError.HostRequired));
        else if (IPAddress.TryParse(host, out _))
            issues.Add(new(SettingsError.HostMustNotBeIpAddress));
        else if (!host.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.'))
            issues.Add(new(SettingsError.HostInvalidCharacters));

        var ip = draft.HostIp.Trim();
        if (ip.Length > 0 && !IPAddress.TryParse(ip, out _))
            issues.Add(new(SettingsError.HostIpInvalid, ip));

        if (!int.TryParse(draft.Port.Trim(), out var port) || port is < 1 or > 65535)
            issues.Add(new(SettingsError.PortOutOfRange));

        var user = draft.User.Trim();
        if (user.Length == 0)
            issues.Add(new(SettingsError.UserRequired));
        else if (!user.Contains('@'))
            issues.Add(new(SettingsError.UserNotUpn));

        // Tomt interface er gyldig: da styrer ikke appen VPN, og går rett på RDP.
        var iface = draft.VpnInterface.Trim();
        if (iface.Length > MaxInterfaceNameLength)
            issues.Add(new(SettingsError.VpnInterfaceTooLong));
        else if (!iface.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            issues.Add(new(SettingsError.VpnInterfaceInvalidCharacters));

        if (!int.TryParse(draft.HandshakeTimeout.Trim(), out var timeout) || timeout is < 5 or > 600)
            issues.Add(new(SettingsError.HandshakeTimeoutOutOfRange));

        var args = draft.ExtraArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (args.Length == 0)
            issues.Add(new(SettingsError.ExtraArgsEmpty));
        else if (!args.Contains("/sec:aad"))
            issues.Add(new(SettingsError.ExtraArgsMissingAadFlag));

        return issues;
    }
}
