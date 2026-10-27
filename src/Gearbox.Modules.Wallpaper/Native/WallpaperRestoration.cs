using System.Security.Cryptography;
using System.Text;
using Gearbox.Core;
using Gearbox.Core.Settings;
using Gearbox.Interop;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.Wallpaper.Native;

/// <summary>Guarda cada monitor antes de alterá-lo e mantém em disco restaurações que ainda não deram certo.</summary>
internal sealed class WallpaperRestoration
{
    private readonly IDesktopWallpaperBackend _api;
    private readonly ISettingsService<WallpaperRestoreState> _store;
    private readonly string _backupDirectory;
    private readonly ILogger _logger;
    private WallpaperRestoreState _state;

    public WallpaperRestoration(IDesktopWallpaperBackend api, ISettingsService<WallpaperRestoreState> store, GearboxPaths paths, ILogger logger)
    {
        _api = api;
        _store = store;
        _backupDirectory = Path.Combine(paths.RootDirectory, "wallpaper");
        _logger = logger;
        _state = store.Load();
    }

    /// <summary>Retorna falso quando o fundo original não pode ser restaurado com segurança.</summary>
    public bool TryApplyImage(string devicePath, string imagePath)
    {
        if (_api.HasSlideshow)
        {
            return false;
        }

        var original = _api.GetWallpaper(devicePath);
        if (string.Equals(original, imagePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Find(devicePath) is null)
        {
            // Cor sólida, leitura indisponível ou arquivo ausente: não substituir um estado sem recuperação.
            if (string.IsNullOrWhiteSpace(original) || !File.Exists(original))
            {
                return false;
            }

            Directory.CreateDirectory(_backupDirectory);
            var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(devicePath)))[..16];
            var backup = Path.Combine(_backupDirectory, $"original-{name}{Path.GetExtension(original)}");
            // Se não for possível copiar/salvar, a exceção ocorre antes de mudar o Windows.
            if (!string.Equals(Path.GetFullPath(original), Path.GetFullPath(backup), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(original, backup, overwrite: true);
            }

            var next = CopyState();
            next.Fit ??= _api.GetFit();
            next.Monitors[devicePath] = new RestoreEntry(original, backup);
            Persist(next);
        }

        _api.SetWallpaper(devicePath, imagePath);
        return true;
    }

    public void SetFit(WallpaperFit fit)
    {
        var original = _api.GetFit();
        if (original == fit)
        {
            return;
        }

        if (_state.Fit is null)
        {
            var next = CopyState();
            next.Fit = original;
            Persist(next);
        }

        _api.SetFit(fit);
    }

    public void RestoreFit()
    {
        if (_state.Fit is { } fit && _api.GetFit() != fit)
        {
            _api.SetFit(fit);
        }
    }

    public bool RestoreMonitor(string devicePath)
    {
        var entry = Find(devicePath);
        if (entry is null)
        {
            return true;
        }

        var image = File.Exists(entry.OriginalPath) ? entry.OriginalPath : File.Exists(entry.BackupPath) ? entry.BackupPath : null;
        if (image is null)
        {
            _logger.LogWarning("Restauração pendente no monitor {Monitor}: imagem original e cópia indisponíveis", devicePath);
            return false;
        }

        if (!string.Equals(_api.GetWallpaper(devicePath), image, StringComparison.OrdinalIgnoreCase))
        {
            _api.SetWallpaper(devicePath, image);
        }

        return true;
    }

    public void RestoreAll(IEnumerable<string> connectedMonitors)
    {
        var connected = new HashSet<string>(connectedMonitors, StringComparer.OrdinalIgnoreCase);
        foreach (var devicePath in _state.Monitors.Keys.ToArray())
        {
            if (!connected.Contains(devicePath))
            {
                continue; // O snapshot sobrevive à desconexão e ao encerramento do app.
            }

            try
            {
                if (RestoreMonitor(devicePath))
                {
                    var next = CopyState();
                    next.Monitors.Remove(devicePath);
                    Persist(next);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Restauração pendente no monitor {Monitor}", devicePath);
            }
        }

        try
        {
            RestoreFit();
            var next = CopyState();
            next.Fit = null;
            Persist(next);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Restauração do ajuste do wallpaper pendente");
        }
    }

    private RestoreEntry? Find(string devicePath) => _state.Monitors
        .FirstOrDefault(e => string.Equals(e.Key, devicePath, StringComparison.OrdinalIgnoreCase)).Value;

    private WallpaperRestoreState CopyState() => new()
    {
        Fit = _state.Fit,
        Monitors = new Dictionary<string, RestoreEntry>(_state.Monitors, StringComparer.OrdinalIgnoreCase),
    };

    private void Persist(WallpaperRestoreState next)
    {
        _store.Save(next);
        _state = next;
    }
}
