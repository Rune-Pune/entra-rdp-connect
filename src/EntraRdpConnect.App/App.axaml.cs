using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EntraRdpConnect.App.ViewModels;
using EntraRdpConnect.App.Views;

namespace EntraRdpConnect.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Bygg på en bakgrunnstråd så awaits ikke fanger UI-SynchronizationContext-en
            // (blokkering på UI-tråden mens continuation venter på samme tråd = vranglås).
            var mainViewModel = Task.Run(AppComposition.BuildAsync).GetAwaiter().GetResult();
            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };
        }

        base.OnFrameworkInitializationCompleted();
    }
}