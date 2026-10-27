using Gearbox.Core;
using Gearbox.Modules.Dock.UI;
using Microsoft.Extensions.DependencyInjection;

namespace Gearbox.Modules.Dock;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDockModule(this IServiceCollection services)
    {
        services.AddGearboxSettings<DockSettings>(DockModule.ModuleId);
        services.AddGearboxModule<DockModule, DockSettingsPage>();
        return services;
    }
}
