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

/// <summary>
/// Validerer innstillingene før de lagres. Reglene er hentet fra feil vi faktisk har møtt —
/// særlig at verten må oppgis som NAVN: skriver man en IP, kapper FreeRDP den ved første punktum
/// og Entra svarer AADSTS293004, en feilmelding det er nær umulig å gjette seg fram fra.
///
/// Ren funksjon uten I/O, så reglene kan enhetstestes.
/// </summary>
public static class SettingsValidator
{
    public static IReadOnlyList<string> Validate(SettingsDraft draft)
    {
        var errors = new List<string>();

        var host = draft.Host.Trim();
        if (host.Length == 0)
            errors.Add("Vertsmaskin (navn) må fylles ut.");
        else if (IPAddress.TryParse(host, out _))
            errors.Add("Vertsmaskinen må oppgis som NAVN, ikke IP-adresse. Med IP kapper FreeRDP " +
                       "adressen ved første punktum, og Entra avviser innloggingen. Legg IP-en i " +
                       "feltet under i stedet.");
        else if (!host.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.'))
            errors.Add("Vertsnavnet kan bare inneholde bokstaver, tall, bindestrek og punktum.");

        var ip = draft.HostIp.Trim();
        if (ip.Length > 0 && !IPAddress.TryParse(ip, out _))
            errors.Add($"«{ip}» er ikke en gyldig IP-adresse.");

        if (!int.TryParse(draft.Port.Trim(), out var port) || port is < 1 or > 65535)
            errors.Add("Port må være et tall mellom 1 og 65535 (standard for RDP er 3389).");

        var user = draft.User.Trim();
        if (user.Length == 0)
            errors.Add("Bruker (UPN) må fylles ut.");
        else if (!user.Contains('@'))
            errors.Add("Bruker må være en full UPN, f.eks. deg@firma.no.");

        // Tomt interface er gyldig: da styrer ikke appen VPN, og går rett på RDP.
        var iface = draft.VpnInterface.Trim();
        if (iface.Length > 15)
            errors.Add("VPN-interface kan være høyst 15 tegn (grensen for nettverksnavn i Linux).");
        else if (!iface.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            errors.Add("VPN-interface kan bare inneholde bokstaver, tall, bindestrek, understrek og punktum.");

        if (!int.TryParse(draft.HandshakeTimeout.Trim(), out var timeout) || timeout is < 5 or > 600)
            errors.Add("Handshake-timeout må være mellom 5 og 600 sekunder.");

        var args = draft.ExtraArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (args.Length == 0)
            errors.Add("xfreerdp3-argumentene kan ikke være tomme.");
        else if (!args.Contains("/sec:aad"))
            errors.Add("Argumentene må inneholde /sec:aad — det er eneste vei inn på en " +
                       "Entra-tilknyttet maskin.");

        return errors;
    }
}
