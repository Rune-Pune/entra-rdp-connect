namespace EntraRdpConnect.Infrastructure.Browser;

/// <summary>
/// Felles håndtering av den dedikerte nettleserprofilen som brukes til Entra-innloggingen.
///
/// Profilen gjenbrukes med vilje, så MFA kan hoppes over ved senere oppkoblinger. Det betyr at den
/// inneholder en innlogget sesjon (cookies) og de fangede <c>code=</c>-adressene — innhold ingen
/// andre brukere på maskinen skal kunne lese.
/// </summary>
internal static class BrowserProfile
{
    private const UnixFileMode DirMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode SecretMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Mapper vi ikke bryr oss om: de inneholder bare mellomlagrede nettsider, og kan
    /// vokse til tusenvis av filer. Å traversere dem ville lagt unødig ventetid rett foran et
    /// innloggingsvindu som utløper på under ett minutt.</summary>
    private static readonly string[] SkippedDirectories =
        ["Cache", "Code Cache", "GPUCache", "ShaderCache", "DawnCache", "GrShaderCache",
         "component_crx_cache", "startupCache", "cache2", "thumbnails"];

    /// <summary>Oppretter profilmappa med trange rettigheter, og strammer eventuelt innhold
    /// nettleseren har lagt igjen fra tidligere økter.</summary>
    public static void Prepare(string profileDir)
    {
        Directory.CreateDirectory(profileDir, DirMode);
        Harden(profileDir);
    }

    /// <summary>
    /// Setter 0700 på mapper og 0600 på filer i profilen. Nettleseren lager sine egne filer med
    /// sine egne rettigheter (ofte 0644), så dette kjøres både før den starter og etter at den er
    /// avsluttet — ellers ville filer laget under økta ligget åpne fram til neste innlogging.
    /// </summary>
    internal static void Harden(string profileDir)
    {
        if (!Directory.Exists(profileDir))
            return;

        TrySetMode(profileDir, DirMode);
        HardenChildren(profileDir);
    }

    private static void HardenChildren(string dir)
    {
        foreach (var entry in SafeEntries(dir))
        {
            if (Directory.Exists(entry))
            {
                TrySetMode(entry, DirMode);
                if (!IsSkipped(entry))
                    HardenChildren(entry);
            }
            else
            {
                TrySetMode(entry, SecretMode);
            }
        }
    }

    private static bool IsSkipped(string dir)
        => SkippedDirectories.Contains(Path.GetFileName(dir), StringComparer.OrdinalIgnoreCase);

    /// <summary>Leser innholdet i én mappe. Materialiseres med en gang, slik at et unntak fanges
    /// her og ikke først når kalleren begynner å iterere.</summary>
    private static string[] SafeEntries(string dir)
    {
        try
        {
            return Directory.GetFileSystemEntries(dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void TrySetMode(string path, UnixFileMode mode)
    {
        // Best effort: en enkelt fil vi ikke rår over skal ikke stoppe innloggingen.
        try { File.SetUnixFileMode(path, mode); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
