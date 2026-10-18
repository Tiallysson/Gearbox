using Gearbox.Core;
using Gearbox.Interop;
using Gearbox.Modules.RoundedMaximize.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gearbox.Modules.RoundedMaximize;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoundedMaximizeModule(this IServiceCollection services)
    {
        services.TryAddSingleton<WinEventHookManager>();
        services.AddGearboxSettings<RoundedMaximizeSettings>(RoundedMaximizeModule.ModuleId);
        services.TryAddTransient<RoundedMaximizeSettingsViewModel>();
        services.AddGearboxModule<RoundedMaximizeModule, RoundedMaximizeSettingsPage>();
        return services;
    }
}
