using System.Diagnostics.CodeAnalysis;

namespace EntraRdpConnect.Core.Application;

/// <summary>
/// Validerer en retur-URL brukeren limer inn manuelt. Ren funksjon uten UI-avhengigheter, så
/// reglene kan enhetstestes — en feil her ville ellers først vist seg midt i en innlogging.
/// </summary>
public static class RedirectUrlValidator
{
    public static bool TryParse(string? text, [NotNullWhen(true)] out Uri? url)
    {
        url = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed))
            return false;

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            return false;

        // Selve poenget med adressen er autorisasjonskoden.
        if (!parsed.Query.Contains("code=", StringComparison.OrdinalIgnoreCase))
            return false;

        url = parsed;
        return true;
    }
}
