using Darwin.Core.Modules;
using Darwin.Core.Settings;
using Darwin.Core.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Darwin.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Serviços compartilhados: caminhos, configurações, inicialização com o Windows e módulos.</summary>
    public static IServiceCollection AddDarwinCore(this IServiceCollection services, DarwinPaths? paths = null)
    {
        services.TryAddSingleton(paths ?? DarwinPaths.Default);
        services.TryAddSingleton<IRunKeyStore, CurrentUserRunKeyStore>();
        services.TryAddSingleton<IStartupService>(sp => new RegistryStartupService(
            sp.GetRequiredService<IRunKeyStore>(),
            sp.GetRequiredService<ILogger<RegistryStartupService>>()));
        services.TryAddSingleton<ModuleFaultHub>();
        services.TryAddSingleton<IModuleFaultReporter>(sp => sp.GetRequiredService<ModuleFaultHub>());
        services.TryAddSingleton<ModuleManager>();
        services.AddDarwinSettings<ModuleStateSettings>("modules");
        return services;
    }

    /// <summary>Registra <see cref="ISettingsService{T}"/> gravando em <c>settings\{id}.json</c>.</summary>
    public static IServiceCollection AddDarwinSettings<T>(this IServiceCollection services, string id)
        where T : class, new()
    {
        services.TryAddSingleton<ISettingsService<T>>(sp => new JsonSettingsService<T>(
            id,
            sp.GetRequiredService<DarwinPaths>(),
            sp.GetRequiredService<ILogger<JsonSettingsService<T>>>()));
        return services;
    }

    /// <summary>
    /// Registra um módulo como <see cref="IDarwinModule"/> e como ele mesmo (a mesma instância),
    /// junto com a página de configurações.
    /// </summary>
    public static IServiceCollection AddDarwinModule<TModule, TSettingsPage>(this IServiceCollection services)
        where TModule : class, IDarwinModule
        where TSettingsPage : class
    {
        services.TryAddSingleton<TModule>();
        services.AddSingleton<IDarwinModule>(sp => sp.GetRequiredService<TModule>());
        services.TryAddTransient<TSettingsPage>();
        return services;
    }
}
