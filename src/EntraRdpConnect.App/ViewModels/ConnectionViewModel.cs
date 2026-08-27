using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EntraRdpConnect.Core.Application;
using EntraRdpConnect.App.Localization;
using EntraRdpConnect.Core.Domain;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.App.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly ConnectionOrchestrator _orchestrator;
    private readonly string _idleSubtitle;

    private readonly ISystemDependencies? _dependencies;
    private static Localizer L => Localizer.Instance;

    public ConnectionViewModel(
        ConnectionOrchestrator orchestrator,
        string targetDescription,
        ISystemDependencies? dependencies = null,
        ManualCodePrompt? manualPrompt = null)
    {
        _orchestrator = orchestrator;
        _idleSubtitle = targetDescription;
        StatusSubtitle = targetDescription;
        _dependencies = dependencies;
        ManualPrompt = manualPrompt;
        RefreshDependencies();
    }

    /// <summary>Sant når appen styrer VPN-tunnelen selv — ellers er «Koble fra» meningsløs.</summary>
    public bool ManagesVpn => _orchestrator.ManagesVpn;

    /// <summary>Reserveløsning for kodeinnliming når auto-fangst ikke er mulig.</summary>
    public ManualCodePrompt? ManualPrompt { get; }

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
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsConnected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHumanMoment))]
    public partial string? HumanMoment { get; set; }

    /// <summary>True = push-moment (telefon-ikon), false = Firefox-login (nettleser-ikon).</summary>
    [ObservableProperty] public partial bool HumanMomentIsPush { get; set; }

    public bool ShowHumanMoment => HumanMoment is not null;

    public ObservableCollection<string> Log { get; } = [];

    [RelayCommand]
    private async Task ConnectAsync()
    {
        Log.Clear();
        IsConnected = false;
        var vpnProgress = new Progress<VpnPhase>(OnVpnPhase);
        var rdpProgress = new Progress<RdpPhase>(OnRdpPhase);

        try
        {
            await _orchestrator.ConnectAsync(vpnProgress, rdpProgress);
            SetStatus(L["StatusNotConnected"], EndedSubtitle, busy: false, humanMoment: null);
        }
        catch (Exception ex)
        {
            AppendLog("✗ " + Describe(ex));
            SetStatus(L["StatusSomethingWentWrong"], Describe(ex), busy: false, humanMoment: null);
        }
        finally
        {
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
            SetStatus(L["StatusNotConnected"], _idleSubtitle, busy: false, humanMoment: null);
        }
        catch (Exception ex)
        {
            AppendLog("✗ " + Describe(ex));
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
                SetStatus(L["StatusBringingUpVpn"], L["SubtitleApprovePkexec"], busy: true, humanMoment: null);
                AppendLog(L["LogBringingUpVpn"]);
                break;
            case VpnPhase.WaitingForHandshake:
                HumanMomentIsPush = true;
                SetStatus(L["StatusWaitingForHandshake"], "", busy: true, humanMoment: L["PromptApprovePush"]);
                AppendLog(L["LogWaitingForPush"]);
                break;
            case VpnPhase.AlreadyConnected:
                AppendLog(L["LogVpnAlreadyUp"]);
                break;
            case VpnPhase.Connected:
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
                SetStatus(L["StatusStartingRemoteDesktop"], "xfreerdp3", busy: true, humanMoment: null);
                AppendLog(L["LogStartingRdp"]);
                break;
            case RdpPhase.LoginUrlDetected:
                HumanMomentIsPush = false;
                SetStatus(L["StatusSignInInBrowser"], "", busy: true, humanMoment: L["PromptSignIn"]);
                AppendLog(L["LogOpeningBrowser"]);
                break;
            case RdpPhase.SubmittingCode:
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

    /// <summary>Unntakene bærer tekniske engelske meldinger; her formuleres de på brukerens språk.</summary>
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
