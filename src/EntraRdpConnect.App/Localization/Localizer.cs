using System.Globalization;
using System.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using EntraRdpConnect.Core.Application;

namespace EntraRdpConnect.App.Localization;

/// <summary>
/// Tekstene brukeren ser. Engelsk er nøytralspråket; norsk ligger i Strings.nb.resx.
///
/// XAML binder mot indekseren (<c>{Binding [Connect], Source=...}</c>), og siden klassen varsler
/// om endring på hele indekseren, bytter grensesnittet språk uten omstart.
/// </summary>
public sealed partial class Localizer : ObservableObject
{
    private static readonly ResourceManager Resources =
        new("EntraRdpConnect.App.Localization.Strings", typeof(Localizer).Assembly);

    /// <summary>Språk vi faktisk har oversettelser for.</summary>
    public static IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("system", "System"),
        new("en", "English"),
        new("nb", "Norsk"),
    ];

    public static Localizer Instance { get; } = new();

    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    /// <summary>Kulturen tekstene formateres med. Tall og klokkeslett som havner i
    /// grensesnittet skal følge valgt språk, ikke systemets.</summary>
    public static CultureInfo Culture => Instance._culture;

    private Localizer() { }

    /// <summary>Oppslag brukt fra XAML. Ukjent nøkkel gir nøkkelen selv, så feilen synes
    /// i grensesnittet framfor å bli en tom etikett.</summary>
    public string this[string key] => Resources.GetString(key, _culture) ?? $"!{key}!";

    /// <summary>Formatert oppslag, for tekster med plassholdere.</summary>
    public string Format(string key, params object?[] args) => string.Format(_culture, this[key], args);

    /// <summary>Teksten for et valideringsproblem, på brukerens språk.</summary>
    public string Describe(ValidationIssue issue) => issue.Value is null
        ? this[$"Error{issue.Error}"]
        : Format($"Error{issue.Error}", issue.Value);

    /// <summary>Bytter språk. «system» følger operativsystemets innstilling.</summary>
    public void SetLanguage(string code)
    {
        _culture = code == "system" || string.IsNullOrWhiteSpace(code)
            ? CultureInfo.InstalledUICulture
            : CultureInfo.GetCultureInfo(code);

        // Varsler at hele indekseren er endret, slik at alle bindinger oppdateres.
        OnPropertyChanged("Item[]");
    }
}

/// <summary>Et språkvalg slik det vises i Innstillinger.</summary>
public sealed record LanguageOption(string Code, string Name);
