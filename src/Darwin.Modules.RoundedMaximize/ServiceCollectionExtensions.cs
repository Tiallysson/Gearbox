using Darwin.Core;
using Darwin.Interop;
using Darwin.Modules.RoundedMaximize.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Darwin.Modules.RoundedMaximize;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRoundedMaximizeModule(this IServiceCollection services)
    {
        services.TryAddSingleton<WinEventHookManager>();
        services.AddDarwinSettings<RoundedMaximizeSettings>(RoundedMaximizeModule.ModuleId);
        services.TryAddTransient<RoundedMaximizeSettingsViewModel>();
        services.AddDarwinModule<RoundedMaximizeModule, RoundedMaximizeSettingsPage>();
        return services;
    }
}
