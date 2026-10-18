using Gearbox.Core;
using Gearbox.Core.Modules;
using Gearbox.Core.Settings;
using Gearbox.Interop;
using Gearbox.Modules.Wallpaper.Logic;
using Gearbox.Modules.Wallpaper.Native;
using Gearbox.Modules.Wallpaper.UI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Windows.System.Power;

namespace Gearbox.Modules.Wallpaper;

/// <summary>
/// Wallpaper em vídeo (atrás dos ícones) ou imagem (pela API do Windows), por monitor, com o wallpaper do
/// Gearbox como padrão. Pausa o vídeo quando ninguém o vê. Ao desligar, devolve o wallpaper original.
/// </summary>
public sealed class WallpaperModule(
    WinEventHookManager hooks,
    ISettingsService<WallpaperSettings> settings,
    ISettingsService<WallpaperRestoreState> restore,
    GearboxPaths paths,
    IModuleFaultReporter faults,
    ILogger<WallpaperModule> logger) : IGearboxModule
{
    public const string ModuleId = "Wallpaper";

    /// <summary>Espera entre um evento de janela e a reavaliação da pausa (eventos chegam em rajadas).</summary>
    private static readonly TimeSpan CoverageDebounce = TimeSpan.FromMilliseconds(250);

    /// <summary>O Explorer leva um instante para recriar a área de trabalho depois de reiniciar.</summary>
    private static readonly TimeSpan ExplorerRestartDelay = TimeSpan.FromSeconds(2);

    private readonly HashSet<string> _failedVideos = new(StringComparer.OrdinalIgnoreCase);
    private WallpaperEngine? _engine;
    private SystemStateMonitor? _systemState;
    private IDisposable? _subscription;
    private DispatcherQueue? _dispatcher;
    private DispatcherQueueTimer? _coverageTimer;
    private DispatcherQueueTimer? _reattachTimer;
    private WallpaperSettings _current = new();
    private SystemState _state;
    private string _lastCoverage = string.Empty;

    /// <summary>A situação de cada monitor mudou (o que está tocando, problemas). Disparado na thread de UI.</summary>
    public event EventHandler? StatusChanged;

    public string Id => ModuleId;

    public string DisplayName => ModuleStrings.Get("ModuleDisplayName");

    public string Description => ModuleStrings.Get("ModuleDescription");

    public string IconGlyph => ""; // Photo

    public bool IsEnabled { get; private set; }

    public Type SettingsPageType => typeof(WallpaperSettingsPage);

    /// <summary>Resultado da última aplicação (vazio com o módulo desligado).</summary>
    public IReadOnlyList<WallpaperAssignment> Assignments { get; private set; } = [];

    /// <summary>Arquivos do wallpaper embutido, ao lado do Gearbox.exe.</summary>
    public static GearboxWallpaper BuiltIn { get; } = FindBuiltIn();

    public void Start()
    {
        if (IsEnabled)
        {
            return;
        }

        try
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread()
                ?? throw new InvalidOperationException("O módulo precisa ser iniciado na thread de UI.");

            _coverageTimer = _dispatcher.CreateTimer();
            _coverageTimer.Interval = CoverageDebounce;
            _coverageTimer.IsRepeating = false;
            _coverageTimer.Tick += (_, _) => Guard(UpdatePause);

            _reattachTimer = _dispatcher.CreateTimer();
            _reattachTimer.Interval = ExplorerRestartDelay;
            _reattachTimer.IsRepeating = false;
            _reattachTimer.Tick += (_, _) => Guard(() => _engine?.ReattachVideos());

            _engine = new WallpaperEngine(restore, paths, logger);
            _engine.VideoFailed += OnVideoFailed;

            _systemState = new SystemStateMonitor(logger);
            _systemState.SessionLockChanged += OnSessionLockChanged;
            _systemState.DisplayPowerChanged += OnDisplayPowerChanged;
            _systemState.DisplaysChanged += OnDisplaysChanged;
            _systemState.ExplorerRestarted += OnExplorerRestarted;

            PowerManager.PowerSupplyStatusChanged += OnPowerChanged;
            PowerManager.BatteryStatusChanged += OnPowerChanged;
            _state = new SystemState(IsOnBattery(), SessionLocked: false, DisplayOff: false);

            _current = settings.Load();
            settings.Changed += OnSettingsChanged;
            _subscription = hooks.Subscribe(Id, OnWinEvent);

            IsEnabled = true;
            Apply();
        }
        catch
        {
            // Desfaz o que já foi feito; o casco loga e desabilita o módulo.
            IsEnabled = false;
            Cleanup();
            throw;
        }
    }

    public void Stop()
    {
        if (!IsEnabled)
        {
            return;
        }

        // Marca como desligado primeiro: mesmo que a limpeza falhe, o módulo não fica "meio ligado".
        IsEnabled = false;
        Cleanup();
    }

    private void Cleanup()
    {
        _subscription?.Dispose();
        _subscription = null;
        settings.Changed -= OnSettingsChanged;
        PowerManager.PowerSupplyStatusChanged -= OnPowerChanged;
        PowerManager.BatteryStatusChanged -= OnPowerChanged;

        _coverageTimer?.Stop();
        _reattachTimer?.Stop();

        if (_systemState is not null)
        {
            _systemState.SessionLockChanged -= OnSessionLockChanged;
            _systemState.DisplayPowerChanged -= OnDisplayPowerChanged;
            _systemState.DisplaysChanged -= OnDisplaysChanged;
            _systemState.ExplorerRestarted -= OnExplorerRestarted;
            _systemState.Dispose();
            _systemState = null;
        }

        var engine = _engine;
        _engine = null;
        if (engine is not null)
        {
            engine.VideoFailed -= OnVideoFailed;
            try
            {
                engine.RestoreAll();
            }
            finally
            {
                engine.Dispose();
            }
        }

        _failedVideos.Clear();
        Assignments = [];
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Apply()
    {
        if (_engine is null)
        {
            return;
        }

        Assignments = WallpaperResolver.Resolve(
            _current,
            _engine.Monitors.Select(m => m.DevicePath),
            BuiltIn,
            File.Exists,
            _failedVideos);

        foreach (var problem in Assignments.Where(a => a.Problem != WallpaperProblem.None))
        {
            logger.LogWarning("Monitor {Monitor}: escolha descartada ({Problem})", problem.DevicePath, problem.Problem);
        }

        _engine.Apply(Assignments, _current.Fit);
        UpdatePause();
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pausa ou retoma cada vídeo conforme a <see cref="PausePolicy"/>.</summary>
    private void UpdatePause()
    {
        if (_engine is null || _engine.VideoMonitors.Count == 0)
        {
            return;
        }

        IReadOnlySet<string> covered = new HashSet<string>();
        if (_current.PauseOnFullscreen)
        {
            var monitors = _engine.Monitors
                .Select(m => new MonitorArea(m.DevicePath, m.Bounds, NativeWindow.TryGetWorkAreaForRect(m.Bounds, out var work) ? work : m.Bounds))
                .ToList();
            var ownProcess = (uint)Environment.ProcessId;
            var windows = NativeWindow.EnumerateTopLevelWindows().Select(w => new DesktopWindow(
                w.Bounds,
                w.ClassName,
                IsVisible: true,
                w.IsMinimized,
                w.IsCloaked,
                w.IsToolWindow,
                w.IsClickThrough,
                IsOwnProcess: w.ProcessId == ownProcess));
            var covering = CoverageRules.CoveringWindows(windows, monitors);
            covered = new HashSet<string>(covering.Keys, StringComparer.OrdinalIgnoreCase);

            var coverage = string.Join("; ", covering.Select(c => $"{c.Key} coberto por {c.Value.ClassName} {c.Value.Bounds}"));
            if (coverage != _lastCoverage)
            {
                _lastCoverage = coverage;
                logger.LogDebug("Cobertura: {Coverage}", coverage.Length == 0 ? "nenhum monitor coberto" : coverage);
            }
        }

        foreach (var devicePath in _engine.VideoMonitors.ToList())
        {
            _engine.SetPaused(devicePath, PausePolicy.IsPaused(_current, _state, covered.Contains(devicePath)));
        }
    }

    private void OnWinEvent(object? sender, WinEventArgs e)
    {
        // Rajadas de eventos (arrastar uma janela gera dezenas): reavalia uma vez, um pouco depois.
        if (_engine is { VideoMonitors.Count: > 0 } && _coverageTimer is not null)
        {
            _coverageTimer.Stop();
            _coverageTimer.Start();
        }
    }

    private void OnSettingsChanged(object? sender, WallpaperSettings e) => Guard(() =>
    {
        _current = e;
        _failedVideos.Clear(); // Nova escolha: dá outra chance aos vídeos que falharam.
        Apply();
    });

    private void OnVideoFailed(object? sender, VideoFailedEventArgs e) => Guard(() =>
    {
        _failedVideos.Add(e.Path);
        Apply();
    });

    private void OnSessionLockChanged(object? sender, bool locked) => Guard(() =>
    {
        _state = _state with { SessionLocked = locked };
        UpdatePause();
    });

    private void OnDisplayPowerChanged(object? sender, bool on) => Guard(() =>
    {
        _state = _state with { DisplayOff = !on };
        UpdatePause();
    });

    private void OnDisplaysChanged(object? sender, EventArgs e) => Guard(() =>
    {
        _engine?.RefreshMonitors();
        _engine?.ReattachVideos();
        Apply();
    });

    private void OnExplorerRestarted(object? sender, EventArgs e) => Guard(() =>
    {
        _reattachTimer?.Stop();
        _reattachTimer?.Start();
    });

    private void OnPowerChanged(object? sender, object e)
    {
        // Chega numa thread qualquer.
        _dispatcher?.TryEnqueue(() => Guard(() =>
        {
            _state = _state with { OnBattery = IsOnBattery() };
            UpdatePause();
        }));
    }

    /// <summary>
    /// O conteúdo de bibliotecas WinUI vai para <c>Gearbox.Modules.Wallpaper\Assets</c> no build, mas para
    /// <c>Assets</c> no publish: procura nos dois.
    /// </summary>
    private static GearboxWallpaper FindBuiltIn()
    {
        string[] folders =
        [
            Path.Combine(AppContext.BaseDirectory, "Assets", "Wallpapers"),
            Path.Combine(AppContext.BaseDirectory, typeof(WallpaperModule).Assembly.GetName().Name!, "Assets", "Wallpapers"),
        ];

        var folder = folders.FirstOrDefault(f => File.Exists(Path.Combine(f, "Gearbox.jpg"))) ?? folders[0];
        return new GearboxWallpaper(Path.Combine(folder, "Gearbox.jpg"), Path.Combine(folder, "Gearbox.mp4"));
    }

    /// <summary>Na bateria = tem bateria e a fonte não está fornecendo energia suficiente.</summary>
    private static bool IsOnBattery() =>
        PowerManager.BatteryStatus != BatteryStatus.NotPresent &&
        PowerManager.PowerSupplyStatus != PowerSupplyStatus.Adequate;

    private void Guard(Action action)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception ex)
        {
            faults.ReportFault(Id, ex);
        }
    }
}
