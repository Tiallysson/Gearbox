using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Gearbox.Core;
using Gearbox.Core.Settings;
using Gearbox.Interop;
using Gearbox.Modules.Wallpaper.Logic;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.Wallpaper.Native;

/// <summary>
/// Aplica as decisões do <see cref="WallpaperResolver"/>: imagem pelo <c>IDesktopWallpaper</c> do Windows,
/// vídeo por <see cref="VideoWallpaperWindow"/>. Guarda o wallpaper original antes da primeira mudança e o
/// devolve em <see cref="RestoreAll"/>. Só pode ser usado na thread de UI.
/// </summary>
internal sealed class WallpaperEngine : IDisposable
{
    private readonly ISettingsService<WallpaperRestoreState> _restore;
    private readonly string _backupDirectory;
    private readonly ILogger _logger;
    private readonly DesktopWallpaper _api = new();
    private readonly Dictionary<string, VideoWallpaperWindow> _videos = new(StringComparer.OrdinalIgnoreCase);
    private WallpaperRestoreState _restoreState;

    public WallpaperEngine(ISettingsService<WallpaperRestoreState> restore, GearboxPaths paths, ILogger logger)
    {
        _restore = restore;
        _backupDirectory = Path.Combine(paths.RootDirectory, "wallpaper");
        _logger = logger;
        _restoreState = restore.Load();
        Monitors = _api.GetMonitors();
    }

    /// <summary>Um vídeo falhou ao tocar; o monitor já voltou para o wallpaper estático.</summary>
    public event EventHandler<VideoFailedEventArgs>? VideoFailed;

    public IReadOnlyList<WallpaperMonitor> Monitors { get; private set; }

    /// <summary>Device paths dos monitores que estão com vídeo.</summary>
    public IReadOnlyCollection<string> VideoMonitors => _videos.Keys;

    public void RefreshMonitors() => Monitors = _api.GetMonitors();

    public void Apply(IReadOnlyList<WallpaperAssignment> assignments, WallpaperFit fit)
    {
        foreach (var assignment in assignments)
        {
            switch (assignment.Kind)
            {
                case MediaKind.Image when assignment.Path is not null:
                    RemoveVideo(assignment.DevicePath);
                    SetStatic(assignment.DevicePath, assignment.Path);
                    break;

                case MediaKind.Video when assignment.Path is not null:
                    // Por trás do vídeo fica o wallpaper original: é o que aparece se o Gearbox fechar.
                    RestoreStatic(assignment.DevicePath);
                    ShowVideo(assignment.DevicePath, assignment.Path, fit);
                    break;

                default:
                    RemoveVideo(assignment.DevicePath);
                    RestoreStatic(assignment.DevicePath);
                    break;
            }
        }

        if (assignments.Any(a => a.Kind == MediaKind.Image && a.Path is not null))
        {
            SetFit(fit);
        }
        else
        {
            RestoreFit();
        }

        // Monitores que sumiram (ou que não estão mais na lista).
        foreach (var devicePath in _videos.Keys.Except(assignments.Select(a => a.DevicePath), StringComparer.OrdinalIgnoreCase).ToList())
        {
            RemoveVideo(devicePath);
        }
    }

    public void SetPaused(string devicePath, bool paused)
    {
        if (_videos.TryGetValue(devicePath, out var video) && video.SetPaused(paused))
        {
            _logger.LogDebug("Vídeo do monitor {Monitor} {State}", devicePath, paused ? "pausado" : "tocando");
        }
    }

    /// <summary>Prende os vídeos de novo (o Explorer reiniciou) e corrige as posições (monitores mudaram).</summary>
    public void ReattachVideos()
    {
        foreach (var (devicePath, video) in _videos)
        {
            var monitor = Monitors.FirstOrDefault(m => string.Equals(m.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase));
            if (monitor.DevicePath is null)
            {
                continue;
            }

            video.SetBounds(monitor.Bounds);
        }
    }

    /// <summary>Remove os vídeos e devolve o wallpaper e o ajuste que o Windows tinha antes.</summary>
    public void RestoreAll()
    {
        foreach (var devicePath in _videos.Keys.ToList())
        {
            RemoveVideo(devicePath);
        }

        if (_restoreState.IsEmpty)
        {
            return;
        }

        foreach (var devicePath in _restoreState.Monitors.Keys.ToList())
        {
            RestoreStatic(devicePath);
        }

        RestoreFit();

        _restoreState = new WallpaperRestoreState();
        _restore.Save(_restoreState);
        _logger.LogInformation("Wallpaper original restaurado");
    }

    public void Dispose()
    {
        foreach (var video in _videos.Values)
        {
            video.Dispose();
        }

        _videos.Clear();
        _api.Dispose();
    }

    private void ShowVideo(string devicePath, string path, WallpaperFit fit)
    {
        if (_videos.TryGetValue(devicePath, out var existing))
        {
            if (string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                existing.SetFit(fit);
                return;
            }

            RemoveVideo(devicePath);
        }

        var monitor = Monitors.FirstOrDefault(m => string.Equals(m.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase));
        if (monitor.DevicePath is null)
        {
            return;
        }

        var video = new VideoWallpaperWindow(path, fit, monitor.Bounds, _logger);
        video.Failed += (_, message) => OnVideoFailed(devicePath, path, message);
        _videos[devicePath] = video;
        _logger.LogInformation("Vídeo {Path} no monitor {Monitor}", path, devicePath);
    }

    private void RemoveVideo(string devicePath)
    {
        if (_videos.Remove(devicePath, out var video))
        {
            video.Dispose();
        }
    }

    private void OnVideoFailed(string devicePath, string path, string message)
    {
        _logger.LogWarning("O vídeo {Path} não pôde ser tocado no monitor {Monitor}: {Message}", path, devicePath, message);
        RemoveVideo(devicePath);
        VideoFailed?.Invoke(this, new VideoFailedEventArgs(devicePath, path, message));
    }

    private void SetStatic(string devicePath, string imagePath)
    {
        if (string.Equals(_api.GetWallpaper(devicePath), imagePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EnsureSnapshot();
        _api.SetWallpaper(devicePath, imagePath);
    }

    /// <summary>Volta a imagem original do monitor, se o Gearbox a tiver trocado.</summary>
    private void RestoreStatic(string devicePath)
    {
        var entry = _restoreState.Monitors
            .FirstOrDefault(e => string.Equals(e.Key, devicePath, StringComparison.OrdinalIgnoreCase)).Value;
        if (entry is null)
        {
            return;
        }

        var image = File.Exists(entry.OriginalPath) ? entry.OriginalPath : File.Exists(entry.BackupPath) ? entry.BackupPath : null;
        if (image is null)
        {
            _logger.LogWarning("A imagem original do monitor {Monitor} não existe mais; o wallpaper atual fica", devicePath);
            return;
        }

        if (!string.Equals(_api.GetWallpaper(devicePath), image, StringComparison.OrdinalIgnoreCase))
        {
            _api.SetWallpaper(devicePath, image);
        }
    }

    private void SetFit(WallpaperFit fit)
    {
        if (_api.GetFit() == fit)
        {
            return;
        }

        EnsureSnapshot();
        _api.SetFit(fit);
    }

    private void RestoreFit()
    {
        if (_restoreState.Fit is { } fit && _api.GetFit() != fit)
        {
            _api.SetFit(fit);
        }
    }

    /// <summary>Antes da primeira mudança: guarda (em disco) o wallpaper e o ajuste atuais de cada monitor.</summary>
    private void EnsureSnapshot()
    {
        if (!_restoreState.IsEmpty)
        {
            return;
        }

        var state = new WallpaperRestoreState { Fit = _api.GetFit() };
        foreach (var monitor in Monitors)
        {
            var original = _api.GetWallpaper(monitor.DevicePath);
            state.Monitors[monitor.DevicePath] = new RestoreEntry(original, Backup(monitor.DevicePath, original));
        }

        _restoreState = state;
        _restore.Save(state);
        _logger.LogInformation("Wallpaper original guardado ({Count} monitores)", state.Monitors.Count);
    }

    /// <summary>Copia a imagem original para a pasta do Gearbox (ela pode ser apagada enquanto o módulo está ligado).</summary>
    private string? Backup(string devicePath, string? original)
    {
        if (original is null || !File.Exists(original))
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(_backupDirectory);
            var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(devicePath)))[..16];
            var backup = Path.Combine(_backupDirectory, $"original-{name}{Path.GetExtension(original)}");
            File.Copy(original, backup, overwrite: true);
            return backup;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException)
        {
            _logger.LogWarning(ex, "Não foi possível guardar uma cópia de {Path}", original);
            return null;
        }
    }
}

internal sealed class VideoFailedEventArgs(string devicePath, string path, string message) : EventArgs
{
    public string DevicePath { get; } = devicePath;

    public string Path { get; } = path;

    public string Message { get; } = message;
}
