using System.Diagnostics.CodeAnalysis;

namespace EntraRdpConnect.Core.Application;

/// <summary>
/// Trekker login-URL-en ut av xfreerdp3 sin "Browse to:"-linje. Ren funksjon (ingen I/O), så den
/// er lett å enhetsteste — robust mot WLog-prefiks som "[ts] [INFO][com.freerdp...] - ".
/// </summary>
public static class BrowseUrlParser
{
    private const string Marker = "Browse to:";

    public static bool TryParse(string line, [NotNullWhen(true)] out Uri? url)
    {
        url = null;
        if (string.IsNullOrEmpty(line))
            return false;

        var idx = line.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;

        var rest = line[(idx + Marker.Length)..];

        // Plukk første token som er en absolutt http(s)-URL.
        foreach (var token in rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Uri.TryCreate(token, UriKind.Absolute, out var parsed) &&
                (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            {
                url = parsed;
                return true;
            }
        }

        return false;
    }
}
