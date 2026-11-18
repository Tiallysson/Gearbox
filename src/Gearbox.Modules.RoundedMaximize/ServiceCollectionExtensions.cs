using Gearbox.Core;
using Gearbox.Interop.Windowing;
using Gearbox.Modules.RoundedMaximize.Models;
using Gearbox.Modules.RoundedMaximize.ViewModels;
using Gearbox.Modules.RoundedMaximize.Views.Settings;
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
