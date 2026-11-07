using Gearbox.Core;
using Gearbox.Interop.Windowing;
using Gearbox.Modules.Notch.Native;
using Gearbox.Modules.Notch.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gearbox.Modules.Notch;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotchModule(this IServiceCollection services)
    {
        services.TryAddSingleton<WinEventHookManager>();
        services.AddGearboxSettings<NotchSettings>(NotchModule.ModuleId);
        services.AddGearboxSettings<LinksSettings>(LinksSettings.SettingsId);
        services.TryAddSingleton<LinkIconStore>();
        services.TryAddSingleton<LinkCatalog>();
        services.TryAddSingleton<LinkEditor>();
        services.TryAddTransient<NotchSettingsViewModel>();
        services.AddGearboxModule<NotchModule, NotchSettingsPage>();
        return services;
    }
}
