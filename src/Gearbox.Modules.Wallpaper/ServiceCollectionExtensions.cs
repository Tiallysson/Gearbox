using Gearbox.Core;
using Gearbox.Interop.Windowing;
using Gearbox.Modules.Wallpaper.Models;
using Gearbox.Modules.Wallpaper.Services.Storage;
using Gearbox.Modules.Wallpaper.ViewModels;
using Gearbox.Modules.Wallpaper.Views.Settings;
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
        services.TryAddSingleton<GalleryImageCache>();
        services.TryAddTransient<WallpaperSettingsViewModel>();
        services.AddGearboxModule<WallpaperModule, WallpaperSettingsPage>();
        return services;
    }
}
