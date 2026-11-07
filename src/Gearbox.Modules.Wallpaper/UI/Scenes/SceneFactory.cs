using Gearbox.Modules.Wallpaper.Logic.Gallery;

namespace Gearbox.Modules.Wallpaper.UI.Scenes;

internal static class SceneFactory
{
    public static WallpaperScene Create(string id) => id switch
    {
        GalleryCatalog.LivingGradient => new LivingGradientScene(),
        GalleryCatalog.AuroraFlow => new AuroraFlowScene(),
        GalleryCatalog.Particles => new ParticlesScene(),
        GalleryCatalog.DigitalClock => new DigitalClockScene(),
        GalleryCatalog.FluidWaves => new FluidWavesScene(),
        _ => throw new ArgumentException($"'{id}' não é um wallpaper interativo da galeria.", nameof(id)),
    };
}
