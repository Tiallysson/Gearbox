using System.Text.Json.Serialization;
using Gearbox.Interop;

namespace Gearbox.Modules.Wallpaper;

/// <summary>Gravado em <c>%LocalAppData%\Gearbox\settings\Wallpaper.json</c>.</summary>
public sealed class WallpaperSettings
{
    /// <summary>Monitores sem arquivo escolhido (ou com arquivo inválido) usam o wallpaper do Gearbox.</summary>
    public bool UseGearboxWallpaper { get; set; } = true;

    /// <summary>Usa <see cref="Default"/> em todos os monitores, ignorando <see cref="Monitors"/>.</summary>
    public bool SameForAllMonitors { get; set; } = true;

    /// <summary>Escolha para todos os monitores (ou para os que não têm uma própria).</summary>
    public MonitorWallpaper Default { get; set; } = new();

    /// <summary>Escolha por monitor, indexada pelo device path do <c>IDesktopWallpaper</c>.</summary>
    public Dictionary<string, MonitorWallpaper> Monitors { get; set; } = [];

    /// <summary>Como imagens e vídeos ocupam o monitor. O Windows só tem um ajuste para todos os monitores.</summary>
    public WallpaperFit Fit { get; set; } = WallpaperFit.Fill;

    public bool PauseOnFullscreen { get; set; } = true;

    public bool PauseOnBattery { get; set; } = true;

    /// <summary>Pausa com a sessão bloqueada ou a tela apagada.</summary>
    public bool PauseWhenLocked { get; set; } = true;
}

public sealed class MonitorWallpaper
{
    /// <summary>Imagem ou vídeo; <c>null</c> = nada escolhido.</summary>
    public string? Path { get; set; }
}

/// <summary>
/// Wallpaper que o Windows tinha antes do módulo mexer, gravado em <c>settings\Wallpaper.restore.json</c>.
/// Fica separado das configurações (que a página regrava) e sobrevive a um crash.
/// </summary>
public sealed class WallpaperRestoreState
{
    /// <summary>Device path do monitor → imagem original.</summary>
    public Dictionary<string, RestoreEntry> Monitors { get; set; } = [];

    public WallpaperFit? Fit { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Monitors.Count == 0 && Fit is null;
}

/// <param name="OriginalPath">Onde a imagem estava.</param>
/// <param name="BackupPath">Cópia guardada pelo Gearbox, para o caso de o original ter sido apagado.</param>
public sealed record RestoreEntry(string? OriginalPath, string? BackupPath);
