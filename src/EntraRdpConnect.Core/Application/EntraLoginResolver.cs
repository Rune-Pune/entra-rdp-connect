using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.Core.Application;

/// <summary>
/// Auto-versjonen av resolveren <see cref="Ports.IRdpSession"/> ber om: åpne login-URL-en i Firefox,
/// og fang nativeclient?code=-URL-en fra historikken. Ferskhets-referansen (<c>since</c>) settes til
/// tidspunktet vi åpner nettleseren, så vi ikke griper en gammel kode.
///
/// Komponerer to porter + klokken, så den er testbar med fakes uten ekte Firefox.
/// </summary>
public sealed class EntraLoginResolver
{
    private readonly IBrowserLauncher _browser;
    private readonly IRedirectUrlCapture _capture;
    private readonly ISystemClock _clock;
    private readonly TimeSpan _captureTimeout;

    public EntraLoginResolver(
        IBrowserLauncher browser,
        IRedirectUrlCapture capture,
        ISystemClock clock,
        TimeSpan captureTimeout)
    {
        _browser = browser;
        _capture = capture;
        _clock = clock;
        _captureTimeout = captureTimeout;
    }

    public async Task<Uri> ResolveAsync(Uri loginUrl, CancellationToken ct = default)
    {
        var since = _clock.UtcNow;
        await _browser.OpenAsync(loginUrl, ct).ConfigureAwait(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_captureTimeout);

        return await _capture.CaptureAsync(since, timeout.Token).ConfigureAwait(false);
    }
}
