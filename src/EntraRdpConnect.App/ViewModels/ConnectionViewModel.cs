using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.App.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly ConnectionOrchestrator _orchestrator;
    private readonly string _idleSubtitle;

    private readonly ISystemDependencies? _dependencies;

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
            parts.Add($"Mangler {string.Join(", ", auto)} — kan installeres herfra.");
        if (manual.Count > 0)
            parts.Add($"Mangler {string.Join(", ", manual)} — følger med UniFi Identity og må hentes fra portalen.");

        MissingDependencies = string.Join(" ", parts);
        CanInstallDependencies = auto.Count > 0;
    }

    [RelayCommand]
    private async Task InstallDependenciesAsync()
    {
        if (_dependencies is null) return;

        CanInstallDependencies = false;
        var log = new Progress<string>(AppendLog);
        try
        {
            await _dependencies.InstallAsync(_dependencies.FindMissing(), log);
        }
        catch (Exception ex)
        {
            AppendLog("✗ " + ex.Message);
        }
        finally
        {
            RefreshDependencies();
        }
    }

    [ObservableProperty] public partial string StatusTitle { get; set; } = "Ikke tilkoblet";
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
            SetStatus("Ikke tilkoblet", EndedSubtitle, busy: false, humanMoment: null);
        }
        catch (Exception ex)
        {
            AppendLog("✗ " + ex.Message);
            SetStatus("Noe gikk galt", ex.Message, busy: false, humanMoment: null);
        }
        finally
        {
            IsConnected = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        if (!ManagesVpn) { AppendLog("Ingen VPN å koble ned."); return; }

        AppendLog("Kobler ned VPN-tunnelen…");
        try
        {
            await _orchestrator.DisconnectAsync();
            AppendLog("Tunnelen er tatt ned.");
            SetStatus("Ikke tilkoblet", _idleSubtitle, busy: false, humanMoment: null);
        }
        catch (Exception ex)
        {
            AppendLog("✗ " + ex.Message);
        }
    }

    private void OnVpnPhase(VpnPhase phase)
    {
        switch (phase)
        {
            case VpnPhase.NotConfigured:
                AppendLog("Ingen VPN konfigurert — går rett på fjernskrivebordet.");
                break;
            case VpnPhase.BringingUp:
                SetStatus("Reiser VPN-tunnelen…", "godkjenn pkexec-dialogen", busy: true, humanMoment: null);
                AppendLog("Reiser VPN-tunnelen (pkexec)…");
                break;
            case VpnPhase.WaitingForHandshake:
                HumanMomentIsPush = true;
                SetStatus("Venter på handshake", "", busy: true,
                    humanMoment: "Godkjenn push i UniFi Identity-appen på mobilen");
                AppendLog("Venter på push-godkjenning på mobilen…");
                break;
            case VpnPhase.AlreadyConnected:
                AppendLog("VPN-tunnelen er allerede oppe.");
                break;
            case VpnPhase.Connected:
                SetStatus("VPN oppe", "handshake OK", busy: true, humanMoment: null);
                AppendLog("Handshake OK.");
                break;
        }
    }

    private void OnRdpPhase(RdpPhase phase)
    {
        switch (phase)
        {
            case RdpPhase.Starting:
                SetStatus("Starter fjernskrivebord…", "xfreerdp3", busy: true, humanMoment: null);
                AppendLog("Starter xfreerdp3…");
                break;
            case RdpPhase.LoginUrlDetected:
                HumanMomentIsPush = false;
                SetStatus("Logg inn i nettleseren", "", busy: true,
                    humanMoment: "Logg inn i nettleseren — vi fanger koden automatisk");
                AppendLog("Åpner nettleseren for innlogging…");
                break;
            case RdpPhase.SubmittingCode:
                SetStatus("Tilkoblet", ConnectedSubtitle, busy: false, humanMoment: null);
                IsConnected = true;
                AppendLog("✓ Koden fanget — kobler til.");
                break;
            case RdpPhase.SessionEnded:
                AppendLog("RDP-økten er avsluttet.");
                break;
        }
    }

    private string ConnectedSubtitle =>
        ManagesVpn ? "fjernskrivebordet er åpent · tunnelen står oppe" : "fjernskrivebordet er åpent";

    private string EndedSubtitle =>
        ManagesVpn ? "økten ble avsluttet · tunnelen står oppe" : "økten ble avsluttet";

    private void SetStatus(string title, string subtitle, bool busy, string? humanMoment)
    {
        StatusTitle = title;
        StatusSubtitle = subtitle;
        IsBusy = busy;
        HumanMoment = humanMoment;
    }

    private void AppendLog(string line) => Log.Add($"{DateTime.Now:HH:mm:ss}  {line}");
}
