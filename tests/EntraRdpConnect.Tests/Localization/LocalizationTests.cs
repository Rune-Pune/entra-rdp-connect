using System.Xml.Linq;
using EntraRdpConnect.App.ViewModels;
using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Domain;
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

    /// <summary>
    /// Kodene presentasjonslaget slår opp tekst for, og prefikset de bruker. En ny verdi i en av
    /// disse enum-ene uten tilhørende ressurs ville vist «!FailureXyz!» i grensesnittet.
    /// </summary>
    public static TheoryData<string, string[]> CodesNeedingText => new()
    {
        { "Error", Enum.GetNames<SettingsError>() },
        { "Install", Enum.GetNames<InstallStage>() },
        { "Action", Enum.GetNames<ConnectionAction>() },
        // Unknown er bevisst utelatt: da finnes det ingen forklaring å gi, og
        // presentasjonslaget faller tilbake på den tekniske meldingen fra unntaket.
        { "Failure", [.. Enum.GetNames<CommandFailure>().Where(n => n != nameof(CommandFailure.Unknown))] },
    };

    [Theory]
    [MemberData(nameof(CodesNeedingText))]
    public void Hver_kode_presentasjonslaget_slaar_opp_har_en_tekst(string prefix, string[] names)
    {
        var keys = KeysIn("Strings.resx");
        var missing = names.Select(name => prefix + name).Where(key => !keys.Contains(key)).ToArray();

        Assert.True(missing.Length == 0, "Koder uten tekst: " + string.Join(", ", missing));
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, ".git")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Fant ikke repo-roten.");
    }
}
