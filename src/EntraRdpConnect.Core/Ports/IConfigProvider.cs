using EntraRdpConnect.Core.Configuration;

namespace EntraRdpConnect.Core.Ports;

/// <summary>Port: gir applikasjonskjernen tilgang til konfigurasjonen uten å vite hvor den
/// kommer fra (fil, minne i test, ...).</summary>
public interface IConfigProvider
{
    /// <summary>Hvor konfigurasjonen leses fra — til bruk i status/feilmeldinger.</summary>
    string Location { get; }

    bool Exists { get; }

    Task<AppConfig> GetAsync(CancellationToken ct = default);

    /// <summary>Lagrer konfigurasjonen (fra innstillinger-fanen i GUI-et).</summary>
    Task SaveAsync(AppConfig config, CancellationToken ct = default);
}
