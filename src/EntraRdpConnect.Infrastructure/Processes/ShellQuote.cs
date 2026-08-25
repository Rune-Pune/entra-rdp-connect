namespace EntraRdpConnect.Infrastructure.Processes;

/// <summary>
/// Siterer argumenter som må legges inn i en shell-kommandostreng.
///
/// Vi unngår shell der vi kan (<see cref="ProcessSpec"/> sender argumenter som liste), men
/// `script -c` tar én kommandostreng som kjøres av et shell. Uten sitering ville konfigverdier
/// som «x; rm -rf ~» blitt utført som kommandoer.
/// </summary>
internal static class ShellQuote
{
    /// <summary>
    /// POSIX-sitering: pakk inn i enkle fnutter, og avslutt/gjenåpne rundt eventuelle fnutter i
    /// verdien. Alt annet — mellomrom, semikolon, $, backticks — mister sin betydning inni.
    /// </summary>
    public static string Single(string value)
        => "'" + (value ?? "").Replace("'", "'\\''") + "'";

    /// <summary>Setter sammen en kommandolinje der hvert ledd er sitert hver for seg.</summary>
    public static string Join(IEnumerable<string> parts)
        => string.Join(' ', parts.Select(Single));
}
