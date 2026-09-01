namespace EntraRdpConnect.Core.Application;

/// <summary>
/// Koordinerer Entra-innloggingen for én RDP-økt: når en "Browse to:"-linje dukker opp, kaller
/// den resolveren (som skaffer nativeclient?code=-URL-en) og
/// skriver resultatet til prosessens stdin. Reagerer bare på FØRSTE login-URL.
///
/// Isolert fra prosess-detaljene (mates linjer utenfra, skriver via en delegat) nettopp så denne
/// koordineringen kan enhetstestes uten en ekte xfreerdp3.
/// </summary>
public sealed class RdpLoginHandler
{
    private readonly Func<Uri, CancellationToken, Task<Uri>> _resolveRedirectUrl;
    private readonly Func<string, CancellationToken, Task> _submit;
    private readonly IProgress<RdpPhase>? _progress;
    private readonly CancellationToken _ct;
    private readonly object _gate = new();
    private Task? _pending;

    public RdpLoginHandler(
        Func<Uri, CancellationToken, Task<Uri>> resolveRedirectUrl,
        Func<string, CancellationToken, Task> submit,
        IProgress<RdpPhase>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _resolveRedirectUrl = resolveRedirectUrl;
        _submit = submit;
        _progress = progress;
        _ct = cancellationToken;
    }

    /// <summary>Mates én stdout/stderr-linje. Trygg å kalle mange ganger — bare første login-URL
    /// utløser handling.</summary>
    public void OnLine(string line)
    {
        // The process keeps draining buffered output after it is killed, so a "Browse to:" line
        // written before cancellation can still arrive afterwards. Without this check the user
        // presses Cancel and a browser window opens anyway, for a login they just called off.
        if (_ct.IsCancellationRequested)
            return;

        if (!BrowseUrlParser.TryParse(line, out var loginUrl))
            return;

        lock (_gate)
            _pending ??= RunLoginAsync(loginUrl);
    }

    private async Task RunLoginAsync(Uri loginUrl)
    {
        _progress?.Report(RdpPhase.LoginUrlDetected);
        var redirect = await _resolveRedirectUrl(loginUrl, _ct).ConfigureAwait(false);

        _progress?.Report(RdpPhase.SubmittingCode);
        await _submit(redirect.ToString(), _ct).ConfigureAwait(false);
    }

    /// <summary>Fullføres når innloggingen er håndtert — brukes for å fange feil fra resolver/stdin.</summary>
    public Task Completion => _pending ?? Task.CompletedTask;
}
