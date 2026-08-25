namespace EntraRdpConnect.Infrastructure.Capture;

/// <summary>
/// Finner Firefox sin places.sqlite. Håndterer både snap-Firefox (Ubuntu 24.04:
/// ~/snap/firefox/common/.mozilla/firefox) og vanlig installasjon (~/.mozilla/firefox).
/// </summary>
public static class FirefoxProfileLocator
{
    public static string? FindPlacesDatabasePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] baseDirs =
        [
            Path.Combine(home, "snap", "firefox", "common", ".mozilla", "firefox"),
            Path.Combine(home, ".mozilla", "firefox"),
        ];

        foreach (var baseDir in baseDirs)
        {
            var iniPath = Path.Combine(baseDir, "profiles.ini");
            if (!File.Exists(iniPath))
                continue;

            var profileDir = ParseDefaultProfilePath(File.ReadAllText(iniPath));
            if (profileDir is null)
                continue;

            var places = Path.Combine(baseDir, profileDir, "places.sqlite");
            if (File.Exists(places))
                return places;
        }

        return null;
    }

    /// <summary>Ren parsing av profiles.ini → relativ sti til default-profilen. Foretrekker
    /// [Install*].Default (profilen som faktisk startes), ellers [Profile*] med Default=1,
    /// ellers første [Profile*].</summary>
    internal static string? ParseDefaultProfilePath(string iniText)
    {
        var sections = ParseIni(iniText);

        // 1) [Install...] peker på profilen som faktisk brukes.
        foreach (var (name, keys) in sections)
            if (name.StartsWith("Install", StringComparison.OrdinalIgnoreCase) &&
                keys.TryGetValue("Default", out var installDefault))
                return installDefault;

        // 2) [Profile...] med Default=1.
        foreach (var (name, keys) in sections)
            if (name.StartsWith("Profile", StringComparison.OrdinalIgnoreCase) &&
                keys.GetValueOrDefault("Default") == "1" &&
                keys.TryGetValue("Path", out var defPath))
                return defPath;

        // 3) Første [Profile...].
        foreach (var (name, keys) in sections)
            if (name.StartsWith("Profile", StringComparison.OrdinalIgnoreCase) &&
                keys.TryGetValue("Path", out var firstPath))
                return firstPath;

        return null;
    }

    private static List<(string Section, Dictionary<string, string> Keys)> ParseIni(string text)
    {
        var result = new List<(string, Dictionary<string, string>)>();
        Dictionary<string, string>? current = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == ';')
                continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result.Add((line[1..^1], current));
            }
            else if (current is not null)
            {
                var eq = line.IndexOf('=');
                if (eq > 0)
                    current[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }

        return result;
    }
}
