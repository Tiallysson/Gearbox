using Gearbox.Core.Modules;
using Gearbox.Core.Settings;
using Gearbox.Core.Startup;
using Gearbox.Core.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Gearbox.Core;

public static class ServiceCollectionExtensions
{
    private const string UpdatesHttpClientKey = "Gearbox.Updates";

    /// <summary>Serviços compartilhados: caminhos, configurações, inicialização com o Windows e módulos.</summary>
    public static IServiceCollection AddGearboxCore(this IServiceCollection services, GearboxPaths? paths = null)
    {
        services.TryAddSingleton(paths ?? GearboxPaths.Default);
        services.TryAddSingleton<IRunKeyStore, CurrentUserRunKeyStore>();
        services.TryAddSingleton<IStartupService>(sp => new RegistryStartupService(
            sp.GetRequiredService<IRunKeyStore>(),
            sp.GetRequiredService<ILogger<RegistryStartupService>>()));
        services.TryAddSingleton<ModuleFaultHub>();
        services.TryAddSingleton<IModuleFaultReporter>(sp => sp.GetRequiredService<ModuleFaultHub>());
        services.TryAddSingleton<ModuleManager>();
        services.AddGearboxSettings<ModuleStateSettings>("modules");
        services.AddGearboxUpdates();
        return services;
    }

    private static IServiceCollection AddGearboxUpdates(this IServiceCollection services)
    {
        services.TryAddKeyedSingleton(UpdatesHttpClientKey, (_, _) => CreateUpdatesHttpClient());
        services.TryAddSingleton<IReleaseSource>(sp => new GitHubReleaseSource(sp.GetRequiredKeyedService<HttpClient>(UpdatesHttpClientKey)));
        services.TryAddSingleton(sp => new UpdateDownloader(
            sp.GetRequiredKeyedService<HttpClient>(UpdatesHttpClientKey),
            sp.GetRequiredService<GearboxPaths>(),
            sp.GetRequiredService<ILogger<UpdateDownloader>>()));
        return services;
    }

    private static HttpClient CreateUpdatesHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Gearbox");
        return http;
    }

    /// <summary>Registra <see cref="ISettingsService{T}"/> gravando em <c>settings\{id}.json</c>.</summary>
    public static IServiceCollection AddGearboxSettings<T>(this IServiceCollection services, string id)
        where T : class, new()
    {
        services.TryAddSingleton<ISettingsService<T>>(sp => new JsonSettingsService<T>(
            id,
            sp.GetRequiredService<GearboxPaths>(),
            sp.GetRequiredService<ILogger<JsonSettingsService<T>>>()));
        return services;
    }

    /// <summary>
    /// Registra um módulo como <see cref="IGearboxModule"/> e como ele mesmo (a mesma instância),
    /// junto com a página de configurações.
    /// </summary>
    public static IServiceCollection AddGearboxModule<TModule, TSettingsPage>(this IServiceCollection services)
        where TModule : class, IGearboxModule
        where TSettingsPage : class
    {
        services.TryAddSingleton<TModule>();
        services.AddSingleton<IGearboxModule>(sp => sp.GetRequiredService<TModule>());
        services.TryAddTransient<TSettingsPage>();
        return services;
    }
}
