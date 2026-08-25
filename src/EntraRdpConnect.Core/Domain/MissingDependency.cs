namespace EntraRdpConnect.Core.Domain;

/// <summary>
/// Et systemverktøy appen trenger, men som mangler. <see cref="AptPackage"/> er null når verktøyet
/// ikke kan installeres fra pakkebrønnen (f.eks. UniFi-klienten, som hentes fra Identity-portalen).
/// </summary>
public sealed record MissingDependency(string Command, string Purpose, string? AptPackage)
{
    public bool CanInstallAutomatically => AptPackage is not null;
}
