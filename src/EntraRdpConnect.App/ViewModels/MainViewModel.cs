namespace EntraRdpConnect.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ConnectionViewModel Connection { get; }
    public SettingsViewModel Settings { get; }

    public MainViewModel(ConnectionViewModel connection, SettingsViewModel settings)
    {
        Connection = connection;
        Settings = settings;
    }
}
