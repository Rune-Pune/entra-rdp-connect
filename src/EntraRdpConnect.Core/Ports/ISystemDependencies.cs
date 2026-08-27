using EntraRdpConnect.Core.Domain;

namespace EntraRdpConnect.Core.Ports;

/// <summary>Port: finner manglende systemverktøy og kan installere dem som er tilgjengelige
/// i pakkebrønnen.</summary>
public interface ISystemDependencies
{
    /// <summary>Påkrevde verktøy som ikke ble funnet på PATH.</summary>
    IReadOnlyList<MissingDependency> FindMissing();

    /// <summary>
    /// Installerer de oppgitte verktøyene. Implementasjonen skal kun godta verktøy den selv har
    /// rapportert som installerbare — aldri vilkårlige pakkenavn utenfra.
    /// </summary>
    Task InstallAsync(
        IReadOnlyList<MissingDependency> dependencies,
        IProgress<InstallProgress>? log = null,
        CancellationToken ct = default);
}
