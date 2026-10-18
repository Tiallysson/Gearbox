using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Gearbox.Core.Settings;
using Gearbox.Interop;
using Gearbox.Modules.Wallpaper.Logic;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace Gearbox.Modules.Wallpaper.UI;

public sealed partial class WallpaperSettingsViewModel : ObservableObject
{
    /// <summary>Opções do combo "Ajuste", na ordem em que aparecem.</summary>
    private static readonly WallpaperFit[] s_fits = [WallpaperFit.Fill, WallpaperFit.Fit, WallpaperFit.Stretch, WallpaperFit.Center];

    private readonly ISettingsService<WallpaperSettings> _settings;
    private readonly WallpaperModule _module;
    private readonly ILogger<WallpaperSettingsViewModel> _logger;
    private readonly DispatcherQueueTimer? _saveTimer;
    private readonly Dictionary<string, MonitorWallpaper> _disconnectedMonitors = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _isLoading;

    public WallpaperSettingsViewModel(ISettingsService<WallpaperSettings> settings, WallpaperModule module, ILogger<WallpaperSettingsViewModel> logger)
    {
        _settings = settings;
        _module = module;
        _logger = logger;

        // Toggles e trocas rápidas de arquivo: salva (e aplica) com um pequeno atraso.
        _saveTimer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();
        if (_saveTimer is not null)
        {
            _saveTimer.Interval = TimeSpan.FromMilliseconds(150);
            _saveTimer.IsRepeating = false;
            _saveTimer.Tick += (_, _) => Save();
        }

        _isLoading = true;
        var current = settings.Load();
        UseGearboxWallpaper = current.UseGearboxWallpaper;
        SameForAllMonitors = current.SameForAllMonitors;
        FitIndex = Math.Max(0, Array.IndexOf(s_fits, current.Fit));
        PauseOnFullscreen = current.PauseOnFullscreen;
        PauseOnBattery = current.PauseOnBattery;
        PauseWhenLocked = current.PauseWhenLocked;

        DefaultChoice = new WallpaperChoiceViewModel(null, ModuleStrings.Get("AllMonitorsTitle"), current.Default?.Path, PickFileAsync, ScheduleSave);
        Monitors = new ObservableCollection<WallpaperChoiceViewModel>(LoadMonitors(current));
        _isLoading = false;

        RefreshStatus();
        _module.StatusChanged += OnModuleStatusChanged;
    }

    /// <summary>Janela dona do seletor de arquivos (a página informa ao carregar).</summary>
    public WindowId OwnerWindow { get; set; }

    [ObservableProperty]
    public partial bool UseGearboxWallpaper { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPerMonitor))]
    public partial bool SameForAllMonitors { get; set; }

    public bool IsPerMonitor => !SameForAllMonitors;

    [ObservableProperty]
    public partial int FitIndex { get; set; }

    [ObservableProperty]
    public partial bool PauseOnFullscreen { get; set; }

    [ObservableProperty]
    public partial bool PauseOnBattery { get; set; }

    [ObservableProperty]
    public partial bool PauseWhenLocked { get; set; }

    public WallpaperChoiceViewModel DefaultChoice { get; }

    public ObservableCollection<WallpaperChoiceViewModel> Monitors { get; }

    [ObservableProperty]
    public partial bool IsStatusOpen { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity StatusSeverity { get; set; }

    /// <summary>Grava imediatamente o que estiver pendente e para de ouvir o módulo (ao sair da página).</summary>
    public void Flush()
    {
        _module.StatusChanged -= OnModuleStatusChanged;
        if (_saveTimer?.IsRunning == true)
        {
            _saveTimer.Stop();
            Save();
        }
    }

    partial void OnUseGearboxWallpaperChanged(bool value) => ScheduleSave();

    partial void OnSameForAllMonitorsChanged(bool value) => ScheduleSave();

    partial void OnFitIndexChanged(int value) => ScheduleSave();

    partial void OnPauseOnFullscreenChanged(bool value) => ScheduleSave();

    partial void OnPauseOnBatteryChanged(bool value) => ScheduleSave();

    partial void OnPauseWhenLockedChanged(bool value) => ScheduleSave();

    private IEnumerable<WallpaperChoiceViewModel> LoadMonitors(WallpaperSettings current)
    {
        IReadOnlyList<WallpaperMonitor> monitors;
        try
        {
            using var api = new DesktopWallpaper();
            monitors = api.GetMonitors();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Não foi possível listar os monitores");
            monitors = [];
        }

        // Escolhas de monitores desconectados ficam guardadas para quando eles voltarem.
        foreach (var (devicePath, choice) in current.Monitors)
        {
            if (!monitors.Any(m => string.Equals(m.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase)))
            {
                _disconnectedMonitors[devicePath] = choice;
            }
        }

        var ordered = monitors.OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var monitor = ordered[i];
            var saved = current.Monitors
                .FirstOrDefault(e => string.Equals(e.Key, monitor.DevicePath, StringComparison.OrdinalIgnoreCase)).Value;
            var title = ModuleStrings.Format("MonitorTitleFormat", i + 1, monitor.Bounds.Width, monitor.Bounds.Height);
            yield return new WallpaperChoiceViewModel(monitor.DevicePath, title, saved?.Path, PickFileAsync, ScheduleSave);
        }
    }

    private async Task<string?> PickFileAsync()
    {
        try
        {
            var picker = new FileOpenPicker(OwnerWindow)
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            };

            foreach (var extension in MediaKinds.ImageExtensions.Concat(MediaKinds.VideoExtensions))
            {
                picker.FileTypeFilter.Add(extension);
            }

            var result = await picker.PickSingleFileAsync();
            return result?.Path;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no seletor de arquivos");
            ShowStatus(ModuleStrings.Get("PickerError"), InfoBarSeverity.Error);
            return null;
        }
    }

    private void OnModuleStatusChanged(object? sender, EventArgs e) => RefreshStatus();

    /// <summary>Mostra os problemas da última aplicação (arquivo sumiu, vídeo não tocou...).</summary>
    private void RefreshStatus()
    {
        var problems = _module.Assignments.Where(a => a.Problem != WallpaperProblem.None).ToList();
        if (problems.Count == 0)
        {
            IsStatusOpen = false;
            return;
        }

        var lines = problems.Select(p =>
        {
            var monitor = Monitors.FirstOrDefault(m => string.Equals(m.DevicePath, p.DevicePath, StringComparison.OrdinalIgnoreCase))?.Title ?? p.DevicePath;
            var fallback = p.IsGearboxWallpaper ? ModuleStrings.Get("FallbackGearbox") : ModuleStrings.Get("FallbackOriginal");
            return ModuleStrings.Format("ProblemLineFormat", monitor, ModuleStrings.Get($"Problem{p.Problem}"), fallback);
        });

        ShowStatus(string.Join(Environment.NewLine, lines.Distinct()), InfoBarSeverity.Warning);
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;
    }

    private void ScheduleSave()
    {
        if (_isLoading)
        {
            return;
        }

        if (_saveTimer is null)
        {
            Save();
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Save()
    {
        var monitors = new Dictionary<string, MonitorWallpaper>(_disconnectedMonitors, StringComparer.OrdinalIgnoreCase);
        foreach (var choice in Monitors)
        {
            if (choice.DevicePath is not null && choice.HasFile)
            {
                monitors[choice.DevicePath] = new MonitorWallpaper { Path = choice.Path };
            }
        }

        _settings.Save(new WallpaperSettings
        {
            UseGearboxWallpaper = UseGearboxWallpaper,
            SameForAllMonitors = SameForAllMonitors,
            Default = new MonitorWallpaper { Path = DefaultChoice.HasFile ? DefaultChoice.Path : null },
            Monitors = monitors,
            Fit = s_fits[Math.Clamp(FitIndex, 0, s_fits.Length - 1)],
            PauseOnFullscreen = PauseOnFullscreen,
            PauseOnBattery = PauseOnBattery,
            PauseWhenLocked = PauseWhenLocked,
        });
    }
}
