namespace Gearbox.Modules.Wallpaper.Logic;

/// <summary>Estado do sistema que interessa à pausa do vídeo.</summary>
public readonly record struct SystemState(bool OnBattery, bool SessionLocked, bool DisplayOff);

public static class PausePolicy
{
    /// <summary>Se o vídeo de um monitor deve ficar pausado.</summary>
    public static bool IsPaused(WallpaperSettings settings, SystemState state, bool monitorCovered) =>
        (settings.PauseOnFullscreen && monitorCovered) ||
        (settings.PauseOnBattery && state.OnBattery) ||
        (settings.PauseWhenLocked && (state.SessionLocked || state.DisplayOff));
}
