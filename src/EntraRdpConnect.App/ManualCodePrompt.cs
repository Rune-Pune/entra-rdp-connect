using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.App;

/// <summary>
/// Reserveløsningen når koden ikke kan fanges automatisk (annen nettleser enn Firefox, eller
/// auto-fangst som feiler): åpner innloggingen i standard nettleser og lar brukeren lime inn
/// nativeclient?code=-URL-en selv.
///
/// Broen mellom orkestratorens resolver-delegat og GUI-et — samme mønster som resten av appen:
/// ventingen er en Task, tilstanden er observerbar for binding.
/// </summary>
public sealed partial class ManualCodePrompt : ObservableObject
{
    private readonly IBrowserLauncher _browser;
    private TaskCompletionSource<Uri>? _tcs;

    public ManualCodePrompt(IBrowserLauncher browser) => _browser = browser;

    [ObservableProperty] public partial bool IsAwaiting { get; set; }
    [ObservableProperty] public partial string LoginUrl { get; set; } = "";
    [ObservableProperty] public partial string PastedUrl { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }

    /// <summary>Åpner lenken i standard nettleser og venter på at brukeren limer inn koden.</summary>
    public async Task<Uri> RequestAsync(Uri loginUrl, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _tcs = tcs;
            LoginUrl = loginUrl.ToString();
            PastedUrl = "";
            Error = null;
            IsAwaiting = true;
        });

        try { await _browser.OpenAsync(loginUrl, ct); }
        catch { /* brukeren kan uansett åpne lenken selv */ }

        await using var reg = ct.Register(() => tcs.TrySetCanceled());
        try
        {
            return await tcs.Task;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsAwaiting = false);
        }
    }

    [RelayCommand]
    private void Submit()
    {
        if (!RedirectUrlValidator.TryParse(PastedUrl, out var uri))
        {
            Error = "Lim inn hele adressen som inneholder «code=».";
            return;
        }

        Error = null;
        _tcs?.TrySetResult(uri);
    }

    [RelayCommand]
    private async Task OpenLinkAsync()
    {
        if (Uri.TryCreate(LoginUrl, UriKind.Absolute, out var uri))
            await _browser.OpenAsync(uri);
    }
}
