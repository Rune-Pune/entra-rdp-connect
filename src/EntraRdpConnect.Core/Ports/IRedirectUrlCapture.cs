namespace EntraRdpConnect.Core.Ports;

/// <summary>
/// Port: fanger nativeclient?code=-URL-en automatisk etter innlogging. Returnerer første treff som
/// er nyere enn <paramref name="since"/> (unngår utløpte koder fra tidligere økter). Kansellering
/// (timeout) via <paramref name="ct"/> gir OperationCanceledException.
/// </summary>
public interface IRedirectUrlCapture
{
    Task<Uri> CaptureAsync(DateTimeOffset since, CancellationToken ct = default);
}
