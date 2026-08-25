namespace EntraRdpConnect.Core.Ports;

/// <summary>Port: åpne en URL i nettleseren (Firefox, standard-profil) for Entra-innloggingen.</summary>
public interface IBrowserLauncher
{
    Task OpenAsync(Uri url, CancellationToken ct = default);
}
