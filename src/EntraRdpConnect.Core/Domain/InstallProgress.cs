namespace EntraRdpConnect.Core.Domain;

/// <summary>Hvor langt pakkeinstallasjonen er kommet.</summary>
public enum InstallStage
{
    /// <summary>Ingen av verktøyene som mangler kan hentes fra pakkebrønnen.</summary>
    NothingInstallable,

    /// <summary>Installasjonen er i gang; <see cref="InstallProgress.Packages"/> sier hvilke.</summary>
    Installing,

    /// <summary>Installasjonen er ferdig.</summary>
    Completed,
}

/// <summary>
/// Framdrift fra en pakkeinstallasjon, som data framfor tekst.
///
/// Porten rapporterte tidligere ferdige setninger, noe som gjorde at et adapterlag måtte kjenne
/// brukerens språk. Nå formulerer driverne selv — samme mønster som <c>VpnPhase</c>.
/// </summary>
public sealed record InstallProgress(InstallStage Stage, IReadOnlyList<string> Packages)
{
    public static InstallProgress NothingInstallable { get; } = new(InstallStage.NothingInstallable, []);
    public static InstallProgress Completed { get; } = new(InstallStage.Completed, []);

    public static InstallProgress Installing(IReadOnlyList<string> packages)
        => new(InstallStage.Installing, packages);
}
