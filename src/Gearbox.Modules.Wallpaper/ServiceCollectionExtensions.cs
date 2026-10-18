using Gearbox.Core;
using Gearbox.Interop;
using Gearbox.Modules.Wallpaper.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gearbox.Modules.Wallpaper;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWallpaperModule(this IServiceCollection services)
    {
        services.TryAddSingleton<WinEventHookManager>();
        services.AddGearboxSettings<WallpaperSettings>(WallpaperModule.ModuleId);
        services.AddGearboxSettings<WallpaperRestoreState>(WallpaperModule.ModuleId + ".restore");
        services.TryAddTransient<WallpaperSettingsViewModel>();
        services.AddGearboxModule<WallpaperModule, WallpaperSettingsPage>();
        return services;
    }
}
