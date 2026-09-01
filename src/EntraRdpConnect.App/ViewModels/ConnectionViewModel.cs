using System.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EntraRdpConnect.Core.Application;
using EntraRdpConnect.App.Localization;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.App.ViewModels;

/// <summary>What the primary button does right now. It changes identity with the state
/// rather than leaving several buttons visible at once.</summary>
public enum ConnectionAction
{
    Connect,
    Retry,
    Cancel,
}

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly ConnectionOrchestrator _orchestrator;

    private readonly ISystemDependencies? _dependencies;
    private static Localizer L => Localizer.Instance;

    private readonly ConnectionStep _tunnel = new("StepTunnel");
    private readonly ConnectionStep _phone = new("StepPhone");
    private readonly ConnectionStep _desktop = new("StepDesktop");
    private readonly ConnectionStep _signIn = new("StepSignIn");

    private CancellationTokenSource? _attempt;

    public ConnectionViewModel(
        ConnectionOrchestrator orchestrator,
        string targetDescription,
        ISystemDependencies? dependencies = null,
        ManualCodePrompt? manualPrompt = null)
    {
        _orchestrator = orchestrator;
        Target = targetDescription;
        _dependencies = dependencies;
        ManualPrompt = manualPrompt;

        // Without a VPN the connection starts straight at RDP, and a rail showing tunnel
        // and push approval would promise two steps that never happen.
        Steps = orchestrator.ManagesVpn
            ? [_tunnel, _phone, _desktop, _signIn]
            : [_desktop, _signIn];

        // The labels are static, but the language can change while the app is running. We
        // live exactly as long as the Localizer singleton, so this needs no unsubscribe.
        L.PropertyChanged += OnLanguageChanged;

        RefreshDependencies();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var step in Steps)
            step.RefreshLabel();

        OnPropertyChanged(nameof(ActionLabel));
    }

    /// <summary>Sant når appen styrer VPN-tunnelen selv — ellers er «Koble fra» meningsløs.</summary>
    public bool ManagesVpn => _orchestrator.ManagesVpn;

    /// <summary>The machine we connect to, as shown in the title strip. It is true on every
    /// screen, so it lives in one place instead of being repeated in each state.</summary>
    public string Target { get; }

    /// <summary>Reserveløsning for kodeinnliming når auto-fangst ikke er mulig.</summary>
    public ManualCodePrompt? ManualPrompt { get; }

    /// <summary>Sjekkpunktene i tilkoblingen, i rekkefølge.</summary>
    public IReadOnlyList<ConnectionStep> Steps { get; }

    /// <summary>Beskrivelse av hva som mangler, eller null når alt er på plass.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMissingDependencies))]
    public partial string? MissingDependencies { get; set; }

    /// <summary>Sant når minst ett av de manglende verktøyene kan installeres med apt.</summary>
    [ObservableProperty] public partial bool CanInstallDependencies { get; set; }

    public bool HasMissingDependencies => MissingDependencies is not null;

    private void RefreshDependencies()
    {
        var missing = _dependencies?.FindMissing() ?? [];
        if (missing.Count == 0)
        {
            MissingDependencies = null;
            CanInstallDependencies = false;
            return;
        }

        var auto = missing.Where(m => m.CanInstallAutomatically).Select(m => m.Command).ToList();
        var manual = missing.Where(m => !m.CanInstallAutomatically).Select(m => m.Command).ToList();

        var parts = new List<string>();
        if (auto.Count > 0)
            parts.Add(L.Format("DependenciesMissingInstallable", string.Join(", ", auto)));
        if (manual.Count > 0)
            parts.Add(L.Format("DependenciesMissingManual", string.Join(", ", manual)));

        MissingDependencies = string.Join(" ", parts);
        CanInstallDependencies = auto.Count > 0;
    }

    [RelayCommand]
    private async Task InstallDependenciesAsync()
    {
        if (_dependencies is null) return;

        CanInstallDependencies = false;
        var log = new Progress<InstallProgress>(p => AppendLog(Describe(p)));
        try
        {
            await _dependencies.InstallAsync(_dependencies.FindMissing(), log);
        }
        catch (Exception ex)
        {
            AppendLog("✗ " + Describe(ex));
        }
        finally
        {
            RefreshDependencies();
        }
    }

    [ObservableProperty] public partial string StatusTitle { get; set; } = Localizer.Instance["StatusNotConnected"];
    [ObservableProperty] public partial string StatusSubtitle { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBringTunnelDown), nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPrimaryAction), nameof(IsIdle))]
    public partial bool IsConnected { get; set; }

    /// <summary>True from the moment an attempt fails until the next one starts.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool HasFailed { get; set; }

    /// <summary>Nothing in flight, nothing open, nothing failed — the only state that
    /// deserves the neutral glyph.</summary>
    public bool IsIdle => !IsBusy && !IsConnected && !HasFailed;

    /// <summary>True from the moment the tunnel is up until we have taken it down again.
    /// Separates "the app can manage VPN" from "there is actually a tunnel to bring down".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBringTunnelDown))]
    public partial bool TunnelIsUp { get; set; }

    /// <summary>Shown only when there is a tunnel to take down and nothing is in flight.</summary>
    public bool CanBringTunnelDown => ManagesVpn && TunnelIsUp && !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionLabel), nameof(ActionIsPrimary))]
    public partial ConnectionAction PrimaryAction { get; set; } = ConnectionAction.Connect;

    public string ActionLabel => L[$"Action{PrimaryAction}"];

    /// <summary>Cancel is a withdrawal, not the action the screen exists for.</summary>
    public bool ActionIsPrimary => PrimaryAction is not ConnectionAction.Cancel;

    /// <summary>During an open session there is nothing to start — only Disconnect remains.</summary>
    public bool ShowPrimaryAction => !IsConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHumanMoment))]
    public partial string? HumanMoment { get; set; }

    /// <summary>True = push-moment (telefon-ikon), false = Firefox-login (nettleser-ikon).</summary>
    [ObservableProperty] public partial bool HumanMomentIsPush { get; set; }

    public bool ShowHumanMoment => HumanMoment is not null;

    public ObservableCollection<string> Log { get; } = [];

    /// <summary>The one button that changes meaning: connect, try again, or cancel.</summary>
    [RelayCommand]
    private Task PrimaryActionInvokedAsync()
    {
        if (PrimaryAction is ConnectionAction.Cancel)
        {
            _attempt?.Cancel();
            return Task.CompletedTask;
        }

        return ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        Log.Clear();
        IsConnected = false;
        HasFailed = false;
        ResetSteps();
        PrimaryAction = ConnectionAction.Cancel;

        var vpnProgress = new Progress<VpnPhase>(OnVpnPhase);
        var rdpProgress = new Progress<RdpPhase>(OnRdpPhase);

        using var attempt = new CancellationTokenSource();
        _attempt = attempt;

        try
        {
            await _orchestrator.ConnectAsync(vpnProgress, rdpProgress, attempt.Token);
            ResetSteps();
            SetStatus(L["StatusNotConnected"], EndedSubtitle, busy: false, humanMoment: null);
            PrimaryAction = ConnectionAction.Connect;
        }
        catch (OperationCanceledException)
        {
            ResetSteps();
            AppendLog(L["LogCancelled"]);
            SetStatus(L["StatusCancelled"], EndedSubtitle, busy: false, humanMoment: null);
            PrimaryAction = ConnectionAction.Connect;
        }
        catch (Exception ex)
        {
            MarkWhereItStopped();
            AppendLog("✗ " + Describe(ex));
            SetStatus(L["StatusSomethingWentWrong"], Describe(ex), busy: false, humanMoment: null);
            HasFailed = true;
            PrimaryAction = ConnectionAction.Retry;
        }
        finally
        {
            _attempt = null;
            IsConnected = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        if (!ManagesVpn) { AppendLog(L["LogNoVpnToBringDown"]); return; }

        AppendLog(L["LogBringingDownVpn"]);
        try
        {
            await _orchestrator.DisconnectAsync();
            AppendLog(L["LogVpnDown"]);
            TunnelIsUp = false;
            HasFailed = false;
            ResetSteps();
            SetStatus(L["StatusNotConnected"], "", busy: false, humanMoment: null);
            PrimaryAction = ConnectionAction.Connect;
        }
        catch (Exception ex)
        {
            AppendLog("✗ " + Describe(ex));
        }
    }

    private void ResetSteps()
    {
        foreach (var step in Steps)
            step.State = StepState.Pending;
    }

    /// <summary>After a failure the passed steps stay lit and the step that was waiting is
    /// marked as the last one we reached, so the rail shows <em>where</em> it broke rather than
    /// just that it broke. The amber goes — there is nothing for the user to do there now.</summary>
    private void MarkWhereItStopped()
    {
        foreach (var step in Steps)
        {
            if (step.State is StepState.NeedsYou)
                step.State = StepState.Current;
        }
    }

    private void OnVpnPhase(VpnPhase phase)
    {
        switch (phase)
        {
            case VpnPhase.NotConfigured:
                AppendLog(L["LogNoVpnConfigured"]);
                break;
            case VpnPhase.BringingUp:
                _tunnel.State = StepState.Current;
                SetStatus(L["StatusBringingUpVpn"], L["SubtitleApprovePkexec"], busy: true, humanMoment: null);
                AppendLog(L["LogBringingUpVpn"]);
                break;
            case VpnPhase.WaitingForHandshake:
                _tunnel.State = StepState.Done;
                _phone.State = StepState.NeedsYou;
                // The interface is already up here — only the handshake is left. Setting this
                // at Connected instead would leave a raised tunnel with no way down from the app
                // when the user cancels or the push expires, which is our most common failure.
                TunnelIsUp = true;
                HumanMomentIsPush = true;
                SetStatus(L["StatusWaitingForHandshake"], "", busy: true, humanMoment: L["PromptApprovePush"]);
                AppendLog(L["LogWaitingForPush"]);
                break;
            case VpnPhase.AlreadyConnected:
                _tunnel.State = StepState.Done;
                _phone.State = StepState.Done;
                TunnelIsUp = true;
                AppendLog(L["LogVpnAlreadyUp"]);
                break;
            case VpnPhase.Connected:
                _tunnel.State = StepState.Done;
                _phone.State = StepState.Done;
                TunnelIsUp = true;
                SetStatus(L["StatusVpnUp"], L["SubtitleHandshakeOk"], busy: true, humanMoment: null);
                AppendLog(L["LogHandshakeOk"]);
                break;
        }
    }

    private void OnRdpPhase(RdpPhase phase)
    {
        switch (phase)
        {
            case RdpPhase.Starting:
                _desktop.State = StepState.Current;
                SetStatus(L["StatusStartingRemoteDesktop"], "xfreerdp3", busy: true, humanMoment: null);
                AppendLog(L["LogStartingRdp"]);
                break;
            case RdpPhase.LoginUrlDetected:
                _desktop.State = StepState.Done;
                _signIn.State = StepState.NeedsYou;
                HumanMomentIsPush = false;
                SetStatus(L["StatusSignInInBrowser"], "", busy: true, humanMoment: L["PromptSignIn"]);
                AppendLog(L["LogOpeningBrowser"]);
                break;
            case RdpPhase.SubmittingCode:
                _desktop.State = StepState.Done;
                _signIn.State = StepState.Done;
                SetStatus(L["StatusConnected"], ConnectedSubtitle, busy: false, humanMoment: null);
                IsConnected = true;
                AppendLog("✓ " + L["LogCodeCaptured"]);
                break;
            case RdpPhase.SessionEnded:
                AppendLog(L["LogSessionEnded"]);
                break;
        }
    }

    private string ConnectedSubtitle =>
        ManagesVpn ? L["SubtitleDesktopOpenTunnelUp"] : L["SubtitleDesktopOpen"];

    private string EndedSubtitle =>
        ManagesVpn ? L["SubtitleSessionEndedTunnelUp"] : L["SubtitleSessionEnded"];

    private void SetStatus(string title, string subtitle, bool busy, string? humanMoment)
    {
        StatusTitle = title;
        StatusSubtitle = subtitle;
        IsBusy = busy;
        HumanMoment = humanMoment;
    }

    private void AppendLog(string line) => Log.Add($"{DateTime.Now:HH:mm:ss}  {line}");

    /// <summary>
    /// Brukerens formulering av en feil. Kjernen og adapterne bærer tekniske engelske meldinger;
    /// her velges teksten ut fra typen og dataene unntaket bærer. Faller tilbake på den tekniske
    /// meldingen når vi ikke har noe bedre å si — bedre enn en tom boks.
    /// </summary>
    private static string Describe(Exception ex) => ex switch
    {
        VpnHandshakeTimeoutException t => L.Format("FailureHandshakeTimeout", t.Timeout.TotalSeconds.ToString("0", Localizer.Culture)),
        VpnCommandException { Failure: not CommandFailure.Unknown } c => L[$"Failure{c.Failure}"],
        PrivilegedCommandException { Failure: not CommandFailure.Unknown } p => L[$"Failure{p.Failure}"],
        _ => ex.Message,
    };

    private static string Describe(InstallProgress p) => p.Stage switch
    {
        InstallStage.Installing => L.Format("InstallInstalling", string.Join(", ", p.Packages)),
        InstallStage.Completed => L["InstallCompleted"],
        _ => L["InstallNothingInstallable"],
    };

}
