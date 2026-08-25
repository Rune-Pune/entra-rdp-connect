using Microsoft.Extensions.DependencyInjection;
using EntraRdpConnect.Core.Ports;
using EntraRdpConnect.Infrastructure.Config;
using EntraRdpConnect.Infrastructure.Time;

namespace EntraRdpConnect.Cli;

/// <summary>
/// Composition root: her — og bare her — kobles konkrete adaptere til porter. Resten av koden
/// ser kun grensesnittene. GUI-et har sin egen tilsvarende rot.
/// </summary>
internal static class CompositionRoot
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfigProvider, FileConfigProvider>();
        services.AddSingleton<ISystemClock, SystemClock>();

        return services.BuildServiceProvider();
    }
}
