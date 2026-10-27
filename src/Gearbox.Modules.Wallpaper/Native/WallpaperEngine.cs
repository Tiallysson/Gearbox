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
    private readonly WallpaperRestoration _restoration;
    private readonly ILogger _logger;
    private readonly DesktopWallpaper _api = new();
    private readonly Dictionary<string, VideoWallpaperWindow> _videos = new(StringComparer.OrdinalIgnoreCase);

    public WallpaperEngine(ISettingsService<WallpaperRestoreState> restore, GearboxPaths paths, ILogger logger)
    {
        _logger = logger;
        _restoration = new WallpaperRestoration(_api, restore, paths, logger);
        Monitors = _api.GetMonitors();
    }

    /// <summary>Um vídeo falhou ao tocar; o monitor já voltou para o wallpaper estático.</summary>
    public event EventHandler<VideoFailedEventArgs>? VideoFailed;

    public IReadOnlyList<WallpaperMonitor> Monitors { get; private set; }

    /// <summary>Device paths dos monitores que estão com vídeo.</summary>
    public IReadOnlyCollection<string> VideoMonitors => _videos.Keys;

    public void RefreshMonitors() => Monitors = _api.GetMonitors();

    public IReadOnlyList<WallpaperAssignment> Apply(IReadOnlyList<WallpaperAssignment> assignments, WallpaperFit fit)
    {
        var applied = new List<WallpaperAssignment>();
        foreach (var assignment in assignments)
        {
            switch (assignment.Kind)
            {
                case MediaKind.Image when assignment.Path is not null:
                    RemoveVideo(assignment.DevicePath);
                    if (!_restoration.TryApplyImage(assignment.DevicePath, assignment.Path))
                    {
                        applied.Add(assignment with
                        {
                            Path = null,
                            Kind = MediaKind.Unsupported,
                            IsGearboxWallpaper = false,
                            Problem = WallpaperProblem.OriginalNotRestorable,
                        });
                        continue;
                    }

                    break;

                case MediaKind.Video when assignment.Path is not null:
                    // Por trás do vídeo fica o wallpaper original: é o que aparece se o Gearbox fechar.
                    _restoration.RestoreMonitor(assignment.DevicePath);
                    ShowVideo(assignment.DevicePath, assignment.Path, fit);
                    break;

                default:
                    RemoveVideo(assignment.DevicePath);
                    _restoration.RestoreMonitor(assignment.DevicePath);
                    break;
            }

            applied.Add(assignment);
        }

        if (applied.Any(a => a.Kind == MediaKind.Image && a.Path is not null))
        {
            _restoration.SetFit(fit);
        }
        else
        {
            _restoration.RestoreFit();
        }

        // Monitores que sumiram (ou que não estão mais na lista).
        foreach (var devicePath in _videos.Keys.Except(assignments.Select(a => a.DevicePath), StringComparer.OrdinalIgnoreCase).ToList())
        {
            RemoveVideo(devicePath);
        }

        return applied;
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

        _restoration.RestoreAll(Monitors.Select(m => m.DevicePath));
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
}

internal sealed class VideoFailedEventArgs(string devicePath, string path, string message) : EventArgs
{
    public string DevicePath { get; } = devicePath;

    public string Path { get; } = path;

    public string Message { get; } = message;
}

