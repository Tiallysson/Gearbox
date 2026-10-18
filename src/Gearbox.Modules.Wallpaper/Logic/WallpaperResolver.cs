namespace Gearbox.Modules.Wallpaper.Logic;

/// <summary>Por que a escolha do usuário não pôde ser usada num monitor.</summary>
public enum WallpaperProblem
{
    None,
    FileNotFound,
    UnsupportedFormat,

    /// <summary>O arquivo existe, mas o vídeo não tocou (corrompido, codec ausente...).</summary>
    PlaybackFailed,
}

/// <summary>Arquivos do wallpaper embutido. <see cref="VideoPath"/> é opcional.</summary>
public sealed record GearboxWallpaper(string ImagePath, string? VideoPath);

/// <summary>O que mostrar num monitor.</summary>
/// <param name="Path"><c>null</c> = deixar o wallpaper original do Windows.</param>
/// <param name="Problem">Preenchido quando a escolha do usuário foi descartada (e caiu no padrão).</param>
public sealed record WallpaperAssignment(string DevicePath, string? Path, MediaKind Kind, bool IsGearboxWallpaper, WallpaperProblem Problem)
{
    public bool KeepsOriginal => Path is null;
}

public static class WallpaperResolver
{
    public static IReadOnlyList<WallpaperAssignment> Resolve(
        WallpaperSettings settings,
        IEnumerable<string> devicePaths,
        GearboxWallpaper builtIn,
        Func<string, bool> fileExists,
        IReadOnlySet<string>? failedVideos = null)
    {
        failedVideos ??= new HashSet<string>();
        return devicePaths.Select(devicePath => ResolveMonitor(settings, devicePath, builtIn, fileExists, failedVideos)).ToArray();
    }

    private static WallpaperAssignment ResolveMonitor(
        WallpaperSettings settings,
        string devicePath,
        GearboxWallpaper builtIn,
        Func<string, bool> fileExists,
        IReadOnlySet<string> failedVideos)
    {
        var chosen = ChoiceFor(settings, devicePath);
        var problem = WallpaperProblem.None;

        if (!string.IsNullOrWhiteSpace(chosen))
        {
            var kind = MediaKinds.Detect(chosen);
            if (kind == MediaKind.Unsupported)
            {
                problem = WallpaperProblem.UnsupportedFormat;
            }
            else if (!fileExists(chosen))
            {
                problem = WallpaperProblem.FileNotFound;
            }
            else if (kind == MediaKind.Video && failedVideos.Contains(chosen))
            {
                problem = WallpaperProblem.PlaybackFailed;
            }
            else
            {
                return new WallpaperAssignment(devicePath, chosen, kind, IsGearboxWallpaper: false, WallpaperProblem.None);
            }
        }

        if (!settings.UseGearboxWallpaper)
        {
            return new WallpaperAssignment(devicePath, null, MediaKind.Unsupported, IsGearboxWallpaper: false, problem);
        }

        // O vídeo embutido é opcional: sem ele (ou se sumiu), usa a imagem.
        return builtIn.VideoPath is { } video && fileExists(video) && !failedVideos.Contains(video)
            ? new WallpaperAssignment(devicePath, video, MediaKind.Video, IsGearboxWallpaper: true, problem)
            : new WallpaperAssignment(devicePath, builtIn.ImagePath, MediaKind.Image, IsGearboxWallpaper: true, problem);
    }

    /// <summary>Arquivo escolhido para o monitor: o geral ou, no modo por monitor, o dele (se houver).</summary>
    private static string? ChoiceFor(WallpaperSettings settings, string devicePath)
    {
        if (!settings.SameForAllMonitors)
        {
            // O JSON não preserva o comparador do dicionário: procura sem diferenciar maiúsculas.
            foreach (var (key, value) in settings.Monitors)
            {
                if (string.Equals(key, devicePath, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value?.Path))
                {
                    return value.Path;
                }
            }
        }

        return settings.Default?.Path;
    }
}
