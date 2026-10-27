using Gearbox.Core;
using Gearbox.Interop;
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
        services.TryAddTransient<NotchSettingsViewModel>();
        services.AddGearboxModule<NotchModule, NotchSettingsPage>();
        return services;
    }
}
