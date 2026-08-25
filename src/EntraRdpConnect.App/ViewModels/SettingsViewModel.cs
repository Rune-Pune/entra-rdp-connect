using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EntraRdpConnect.Core.Application;
using EntraRdpConnect.Core.Configuration;
using EntraRdpConnect.Core.Ports;

namespace EntraRdpConnect.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigProvider _provider;
    private readonly IHostResolution? _hostResolution;
    private AppConfig _current;

    public SettingsViewModel(IConfigProvider provider, AppConfig config, IHostResolution? hostResolution = null)
    {
        _provider = provider;
        _current = config;
        _hostResolution = hostResolution;

        Host = config.Rdp.Host;
        HostIp = config.Rdp.HostIp;
        Port = config.Rdp.Port.ToString();
        User = config.Rdp.User;
        ExtraArgs = string.Join(' ', config.Rdp.ExtraArgs);
        VpnInterface = config.Vpn.Interface;
        HandshakeTimeout = config.Vpn.HandshakeTimeoutSeconds.ToString();

        _ = CheckHostAsync();
    }

    [ObservableProperty] public partial string Host { get; set; }
    [ObservableProperty] public partial string HostIp { get; set; }
    [ObservableProperty] public partial string Port { get; set; }
    [ObservableProperty] public partial string User { get; set; }
    [ObservableProperty] public partial string ExtraArgs { get; set; }
    [ObservableProperty] public partial string VpnInterface { get; set; }
    [ObservableProperty] public partial string HandshakeTimeout { get; set; }
    [ObservableProperty] public partial string? SavedMessage { get; set; }

    /// <summary>Punktliste over hva som må rettes før innstillingene kan lagres.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationErrors))]
    public partial string? ValidationErrors { get; set; }

    public bool HasValidationErrors => ValidationErrors is not null;

    /// <summary>Melding om at vertsnavnet ikke kan slås opp — tilkoblingen vil feile uten det.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHostProblem))]
    public partial string? HostProblem { get; set; }

    public bool HasHostProblem => HostProblem is not null;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var draft = new SettingsDraft(Host, HostIp, Port, User, VpnInterface, HandshakeTimeout, ExtraArgs);
        var errors = SettingsValidator.Validate(draft);
        if (errors.Count > 0)
        {
            ValidationErrors = string.Join("\n", errors.Select(e => "• " + e));
            SavedMessage = null;
            return;
        }

        ValidationErrors = null;

        var port = int.Parse(Port.Trim());
        var timeout = int.Parse(HandshakeTimeout.Trim());
        var args = ExtraArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _current = _current with
        {
            Vpn = _current.Vpn with { Interface = VpnInterface.Trim(), HandshakeTimeoutSeconds = timeout },
            Rdp = _current.Rdp with
            {
                Host = Host.Trim(),
                HostIp = HostIp.Trim(),
                Port = port,
                User = User.Trim(),
                ExtraArgs = args.Length > 0 ? args : _current.Rdp.ExtraArgs,
            },
        };

        await _provider.SaveAsync(_current);
        SavedMessage = $"Lagret {DateTime.Now:HH:mm}. Endringer gjelder fra neste oppstart.";
        await CheckHostAsync();
    }

    /// <summary>Sjekker om vertsnavnet kan slås opp — uten det feiler RDP-en, siden FreeRDP må
    /// få et navn og ikke en IP.</summary>
    [RelayCommand]
    private async Task CheckHostAsync()
    {
        if (_hostResolution is null || string.IsNullOrWhiteSpace(Host))
        {
            HostProblem = null;
            return;
        }

        var name = Host.Trim();
        HostProblem = await _hostResolution.CanResolveAsync(name)
            ? null
            : $"Navnet «{name}» kan ikke slås opp. Tilkoblingen krever navn (ikke IP), så det må " +
              "finnes i DNS eller i /etc/hosts.";
    }

    [RelayCommand]
    private async Task AddHostsEntryAsync()
    {
        if (_hostResolution is null) return;

        try
        {
            await _hostResolution.AddHostsEntryAsync(HostIp.Trim(), Host.Trim());
            SavedMessage = "La til oppføring i /etc/hosts.";
            await CheckHostAsync();
        }
        catch (Exception ex)
        {
            HostProblem = ex.Message;
        }
    }
}
