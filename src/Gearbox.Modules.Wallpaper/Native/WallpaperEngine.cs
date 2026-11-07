using Gearbox.Core;
using Gearbox.Core.Settings;
using Gearbox.Interop.Desktop;
using Gearbox.Modules.Wallpaper.Logic;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.Wallpaper.Native;

internal sealed class WallpaperEngine : IDisposable
{
    private readonly WallpaperRestoration _restoration;
    private readonly ILogger _logger;
    private readonly DesktopWallpaper _api = new();
    private readonly Dictionary<string, IAnimatedWallpaper> _animated = new(StringComparer.OrdinalIgnoreCase);

    public WallpaperEngine(ISettingsService<WallpaperRestoreState> restore, GearboxPaths paths, ILogger logger)
    {
        _logger = logger;
        _restoration = new WallpaperRestoration(_api, restore, paths, logger);
        Monitors = _api.GetMonitors();
    }

    public event EventHandler<VideoFailedEventArgs>? VideoFailed;

    public IReadOnlyList<WallpaperMonitor> Monitors { get; private set; }

    public IReadOnlyCollection<string> AnimatedMonitors => _animated.Keys;

    public void RefreshMonitors() => Monitors = _api.GetMonitors();

    public IReadOnlyList<WallpaperAssignment> Apply(IReadOnlyList<WallpaperAssignment> assignments, WallpaperFitMode fit)
    {
        var applied = new List<WallpaperAssignment>();
        foreach (var assignment in assignments)
        {
            switch (assignment.Kind)
            {
                case MediaKind.Image when assignment.Path is not null:
                    RemoveAnimated(assignment.DevicePath);
                    if (!_restoration.TryApplyImage(assignment.DevicePath, assignment.Path))
                    {
                        applied.Add(assignment with
                        {
                            Path = null,
                            Kind = MediaKind.Unsupported,
                            Problem = WallpaperProblem.OriginalNotRestorable,
                        });
                        continue;
                    }

                    break;

                case MediaKind.Video when assignment.Path is not null:
                    _restoration.RestoreMonitor(assignment.DevicePath);
                    ShowVideo(assignment.DevicePath, assignment.Path, fit);
                    break;

                case MediaKind.Pending:
                    break;

                case MediaKind.Scene when assignment.Path is not null:
                    _restoration.RestoreMonitor(assignment.DevicePath);
                    ShowScene(assignment.DevicePath, assignment.Path);
                    break;

                default:
                    RemoveAnimated(assignment.DevicePath);
                    _restoration.RestoreMonitor(assignment.DevicePath);
                    break;
            }

            applied.Add(assignment);
        }

        var images = applied.Where(a => a.Kind == MediaKind.Image && a.Path is not null).ToList();
        if (images.Count > 0)
        {
            _restoration.SetFit((images.All(a => a.IsGallery) ? WallpaperFitMode.Fill : fit).ToWallpaperFit());
        }
        else if (!applied.Any(a => a.IsPending))
        {
            _restoration.RestoreFit();
        }

        foreach (var devicePath in _animated.Keys.Except(assignments.Select(a => a.DevicePath), StringComparer.OrdinalIgnoreCase).ToList())
        {
            RemoveAnimated(devicePath);
        }

        return applied;
    }

    public void SetPaused(string devicePath, bool paused)
    {
        if (_animated.TryGetValue(devicePath, out var animated) && animated.SetPaused(paused))
        {
            _logger.LogDebug("Wallpaper animado do monitor {Monitor} {State}", devicePath, paused ? "pausado" : "rodando");
        }
    }

    public void ReattachAnimated()
    {
        foreach (var (devicePath, animated) in _animated)
        {
            var monitor = Monitors.FirstOrDefault(m => string.Equals(m.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase));
            if (monitor.DevicePath is not null)
            {
                animated.SetBounds(monitor.Bounds);
            }
        }
    }

    public void RestoreAll()
    {
        foreach (var devicePath in _animated.Keys.ToList())
        {
            RemoveAnimated(devicePath);
        }

        _restoration.RestoreAll(Monitors.Select(m => m.DevicePath));
    }

    public void Dispose()
    {
        foreach (var animated in _animated.Values)
        {
            animated.Dispose();
        }

        _animated.Clear();
        _api.Dispose();
    }

    private void ShowVideo(string devicePath, string path, WallpaperFitMode fit)
    {
        if (_animated.TryGetValue(devicePath, out var existing))
        {
            if (existing is VideoWallpaperWindow video && string.Equals(video.Source, path, StringComparison.OrdinalIgnoreCase))
            {
                video.SetFit(fit);
                return;
            }

            RemoveAnimated(devicePath);
        }

        if (FindMonitor(devicePath) is not { } monitor)
        {
            return;
        }

        var created = new VideoWallpaperWindow(path, fit, monitor.Bounds, _logger);
        created.Failed += (_, message) => OnVideoFailed(devicePath, path, message);
        _animated[devicePath] = created;
        _logger.LogInformation("Vídeo {Path} no monitor {Monitor}", path, devicePath);
    }

    private void ShowScene(string devicePath, string sceneId)
    {
        if (_animated.TryGetValue(devicePath, out var existing))
        {
            if (existing is SceneWallpaperWindow && string.Equals(existing.Source, sceneId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RemoveAnimated(devicePath);
        }

        if (FindMonitor(devicePath) is not { } monitor)
        {
            return;
        }

        _animated[devicePath] = new SceneWallpaperWindow(sceneId, monitor.Bounds, _logger);
        _logger.LogInformation("Wallpaper interativo {Scene} no monitor {Monitor}", sceneId, devicePath);
    }

    private WallpaperMonitor? FindMonitor(string devicePath)
    {
        var monitor = Monitors.FirstOrDefault(m => string.Equals(m.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase));
        return monitor.DevicePath is null ? null : monitor;
    }

    private void RemoveAnimated(string devicePath)
    {
        if (_animated.Remove(devicePath, out var animated))
        {
            animated.Dispose();
        }
    }

    private void OnVideoFailed(string devicePath, string path, string message)
    {
        _logger.LogWarning("O vídeo {Path} não pôde ser tocado no monitor {Monitor}: {Message}", path, devicePath, message);
        RemoveAnimated(devicePath);
        VideoFailed?.Invoke(this, new VideoFailedEventArgs(devicePath, path, message));
    }
}

internal sealed class VideoFailedEventArgs(string devicePath, string path, string message) : EventArgs
{
    public string DevicePath { get; } = devicePath;

    public string Path { get; } = path;

    public string Message { get; } = message;
}
