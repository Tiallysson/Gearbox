using Gearbox.Core;
using Gearbox.Interop.Windowing;
using Gearbox.Modules.Notch.Models;
using Gearbox.Modules.Notch.Services.AI;
using Gearbox.Modules.Notch.Services.Links;
using Gearbox.Modules.Notch.Services.Storage;
using Gearbox.Modules.Notch.ViewModels;
using Gearbox.Modules.Notch.Views.Settings;
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
        services.AddGearboxSettings<MixerSettings>(MixerSettings.SettingsId);
        services.AddGearboxSettings<AiUsageSettings>(AiUsageSettings.SettingsId);
        services.AddGearboxSettings<ChatSettings>(ChatSettings.SettingsId);
        services.TryAddSingleton<ChatStore>();
        services.TryAddSingleton<OpenRouterAccount>();
        services.TryAddSingleton<LinkIconStore>();
        services.TryAddSingleton<LinkCatalog>();
        services.TryAddSingleton<LinkEditor>();
        services.TryAddTransient<NotchSettingsViewModel>();
        services.AddGearboxModule<NotchModule, NotchSettingsPage>();
        return services;
    }
}
