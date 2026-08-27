using System.Xml.Linq;
using EntraRdpConnect.Core.Application;
using Xunit;

namespace EntraRdpConnect.Tests.Localization;

/// <summary>
/// Ressursfilene leses som XML framfor gjennom ResourceManager, så testene kan si nøyaktig
/// hvilken nøkkel som mangler — ikke bare at noe ble tomt.
/// </summary>
public sealed class LocalizationTests
{
    private static readonly string ResourceDir =
        Path.Combine(FindRepoRoot(), "src", "EntraRdpConnect.App", "Localization");

    private static HashSet<string> KeysIn(string fileName) =>
        XDocument.Load(Path.Combine(ResourceDir, fileName))
            .Root!.Elements("data")
            .Select(e => e.Attribute("name")!.Value)
            .ToHashSet();

    [Fact]
    public void Norsk_har_oversettelse_for_hver_engelske_noekkel()
    {
        var english = KeysIn("Strings.resx");
        var norwegian = KeysIn("Strings.nb.resx");

        var missing = english.Except(norwegian).OrderBy(k => k).ToArray();
        Assert.True(missing.Length == 0, "Mangler norsk oversettelse for: " + string.Join(", ", missing));
    }

    [Fact]
    public void Norsk_har_ingen_noekler_som_ikke_finnes_paa_engelsk()
    {
        // Engelsk er nøytralspråket — en nøkkel som bare finnes på norsk er en skrivefeil.
        var extra = KeysIn("Strings.nb.resx").Except(KeysIn("Strings.resx")).OrderBy(k => k).ToArray();
        Assert.True(extra.Length == 0, "Nøkler uten engelsk motstykke: " + string.Join(", ", extra));
    }

    [Fact]
    public void Hver_valideringsfeil_har_en_tekst()
    {
        // Uten dette ville en ny SettingsError vist «!ErrorXyz!» i grensesnittet.
        var keys = KeysIn("Strings.resx");
        var missing = Enum.GetNames<SettingsError>()
            .Select(name => "Error" + name)
            .Where(key => !keys.Contains(key))
            .ToArray();

        Assert.True(missing.Length == 0, "Valideringsfeil uten tekst: " + string.Join(", ", missing));
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, ".git")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Fant ikke repo-roten.");
    }
}
