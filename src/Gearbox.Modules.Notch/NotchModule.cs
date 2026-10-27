using Gearbox.Core.Modules;
using Gearbox.Core.Settings;
using Gearbox.Interop;
using Gearbox.Modules.Notch.Logic;
using Gearbox.Modules.Notch.Native;
using Gearbox.Modules.Notch.UI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Windows.System.Power;

namespace Gearbox.Modules.Notch;

/// <summary>
/// "Ilha" no topo do monitor principal, no estilo dos apps de notch do macOS: mostra o que está tocando, avisa
/// mudanças de volume e do carregador e abre ao passar o mouse, com controles de mídia, relógio e bateria e uma aba
/// de controles rápidos (Wi‑Fi, Bluetooth, volume e saída de áudio).
/// Some com app em tela cheia e vira só uma alça quando a janela em foco passa por baixo dela.
/// </summary>
/// <remarks>
/// Os controles rápidos mudam o sistema por pedido do usuário (como as Configurações Rápidas): <see cref="Stop"/> não
/// os desfaz, porque não são estado do módulo.
/// </remarks>
public sealed class NotchModule(
    WinEventHookManager hooks,
    ISettingsService<NotchSettings> settings,
    IModuleFaultReporter faults,
    ILogger<NotchModule> logger) : IGearboxModule
{
    public const string ModuleId = "Notch";

    /// <summary>Tempo com o mouse em cima até abrir (evita abrir ao só passar pelo topo da tela).</summary>
    private static readonly TimeSpan ExpandDelay = TimeSpan.FromMilliseconds(120);

    /// <summary>Tempo com o mouse fora até fechar (perdoa uma saída rápida pela borda).</summary>
    private static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(350);

    private static readonly TimeSpan PeekDuration = TimeSpan.FromSeconds(2);

    /// <summary>Espera entre um evento de janela e a reavaliação (eventos chegam em rajadas).</summary>
    private static readonly TimeSpan OverlapDebounce = TimeSpan.FromMilliseconds(150);

    /// <summary>Com a ilha aberta: relógio e rede de segurança contra um <c>PointerExited</c> perdido.</summary>
    private static readonly TimeSpan ExpandedTick = TimeSpan.FromMilliseconds(500);

    private readonly NotchState _state = new();
    private NotchSettings _current = new();
    private DispatcherQueue? _dispatcher;
    private NotchWindow? _window;
    private SystemStateMonitor? _systemState;
    private AudioVolumeMonitor? _volume;
    private MediaSessionWatcher? _media;
    private ConnectivityWatcher? _connectivity;
    private SystemMonitor? _system;
    private IDisposable? _subscription;
    private bool _storageVisible;
    private int _driveScan;
    private DispatcherQueueTimer? _expandTimer;
    private DispatcherQueueTimer? _collapseTimer;
    private DispatcherQueueTimer? _peekTimer;
    private DispatcherQueueTimer? _overlapTimer;
    private DispatcherQueueTimer? _expandedTimer;
    private NotchVisibility _visibility;
    private bool _sessionLocked;
    private bool _displayOff;
    private bool? _pluggedIn;
    private string? _lastTrackKey;

    public string Id => ModuleId;

    public string DisplayName => ModuleStrings.Get("ModuleDisplayName");

    public string Description => ModuleStrings.Get("ModuleDescription");

    public string IconGlyph => ""; // PreviewLink

    public bool IsEnabled { get; private set; }

    public Type SettingsPageType => typeof(NotchSettingsPage);

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

            _expandTimer = CreateTimer(ExpandDelay, repeating: false, OnExpandElapsed);
            _collapseTimer = CreateTimer(CollapseDelay, repeating: false, OnCollapseElapsed);
            _peekTimer = CreateTimer(PeekDuration, repeating: false, OnPeekElapsed);
            _overlapTimer = CreateTimer(OverlapDebounce, repeating: false, () => Guard(UpdateVisibility));
            _expandedTimer = CreateTimer(ExpandedTick, repeating: true, OnExpandedTick);

            _current = settings.Load();
            _window = new NotchWindow(GetMonitor(), logger);
            _window.View.HoverChanged += OnHoverChanged;
            _window.View.MediaCommandRequested += OnMediaCommandRequested;
            _window.View.QuickActionRequested += OnQuickActionRequested;
            _window.View.VolumeSliderMoved += OnVolumeSliderMoved;
            _window.View.OutputDeviceSelected += OnOutputDeviceSelected;
            _window.View.PageChanged += OnPageChanged;
            _window.View.DriveOpenRequested += OnDriveOpenRequested;
            ApplyAppearance();

            _systemState = new SystemStateMonitor(logger);
            _systemState.SessionLockChanged += OnSessionLockChanged;
            _systemState.DisplayPowerChanged += OnDisplayPowerChanged;
            _systemState.DisplaysChanged += OnDisplaysChanged;

            PowerManager.PowerSupplyStatusChanged += OnPowerChanged;
            PowerManager.RemainingChargePercentChanged += OnPowerChanged;
            PowerManager.BatteryStatusChanged += OnPowerChanged;

            settings.Changed += OnSettingsChanged;
            _subscription = hooks.Subscribe(Id, OnWinEvent);

            IsEnabled = true;
            ApplySources();
            UpdateBattery(peek: false);
            UpdateVisibility();
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
        PowerManager.RemainingChargePercentChanged -= OnPowerChanged;
        PowerManager.BatteryStatusChanged -= OnPowerChanged;

        foreach (var timer in new[] { _expandTimer, _collapseTimer, _peekTimer, _overlapTimer, _expandedTimer })
        {
            timer?.Stop();
        }

        if (_systemState is not null)
        {
            _systemState.SessionLockChanged -= OnSessionLockChanged;
            _systemState.DisplayPowerChanged -= OnDisplayPowerChanged;
            _systemState.DisplaysChanged -= OnDisplaysChanged;
            _systemState.Dispose();
            _systemState = null;
        }

        StopVolume();
        StopMedia();
        StopConnectivity();
        StopSystem();

        if (_window is not null)
        {
            _window.View.PageChanged -= OnPageChanged;
            _window.View.DriveOpenRequested -= OnDriveOpenRequested;
            _window.View.HoverChanged -= OnHoverChanged;
            _window.View.MediaCommandRequested -= OnMediaCommandRequested;
            _window.View.QuickActionRequested -= OnQuickActionRequested;
            _window.View.VolumeSliderMoved -= OnVolumeSliderMoved;
            _window.View.OutputDeviceSelected -= OnOutputDeviceSelected;
            _window.Dispose();
            _window = null;
        }

        _state.SetHovered(false);
        _state.ClearPeek();
        _pluggedIn = null;
        _lastTrackKey = null;
        _storageVisible = false;
        _driveScan++; // Uma leitura de discos ainda em andamento é descartada.
        _sessionLocked = _displayOff = false;
        _visibility = NotchVisibility.Normal;
    }

    /// <summary>Liga ou desliga as fontes (mídia, volume, rádios) conforme as configurações.</summary>
    private void ApplySources()
    {
        // O aviso de volume e o slider da aba "Controles" leem o mesmo monitor.
        var needsVolume = _current.ShowVolume || _current.ShowAudioControls;
        if (needsVolume && _volume is null)
        {
            try
            {
                _volume = new AudioVolumeMonitor(logger);
                _volume.VolumeChanged += OnVolumeChanged;
                _volume.DefaultDeviceChanged += OnDefaultAudioDeviceChanged;
                _volume.DevicesChanged += OnAudioDevicesChanged;
                PushVolume();
            }
            catch (Exception ex)
            {
                // Sem Core Audio (serviço de áudio parado, por exemplo): a ilha funciona sem volume.
                logger.LogWarning(ex, "Não foi possível acompanhar o volume");
                StopVolume();
            }
        }
        else if (!needsVolume)
        {
            StopVolume();
        }

        if (_current.ShowConnectivity && _connectivity is null && _dispatcher is not null)
        {
            _connectivity = new ConnectivityWatcher(_dispatcher, logger);
            _connectivity.Changed += OnConnectivityChanged;
            _ = _connectivity.StartAsync();
        }
        else if (!_current.ShowConnectivity)
        {
            StopConnectivity();
        }

        if (_current.ShowSystemInfo && _system is null && _dispatcher is not null)
        {
            _system = new SystemMonitor(_dispatcher, logger);
            _system.HardwareChanged += OnHardwareChanged;
            _system.SnapshotChanged += OnSystemSnapshotChanged;
        }
        else if (!_current.ShowSystemInfo)
        {
            StopSystem();
        }

        _window?.View.SetTabs(_current.ShowConnectivity, _current.ShowAudioControls, _current.ShowStorage, _current.ShowSystemInfo);
        RefreshOutputDevices();
        UpdatePageSources();

        if (_current.ShowMedia && _media is null && _dispatcher is not null)
        {
            _media = new MediaSessionWatcher(_dispatcher, logger);
            _media.Changed += OnMediaChanged;
            _ = _media.StartAsync();
        }
        else if (!_current.ShowMedia)
        {
            StopMedia();
        }

        if ((!_current.ShowVolume && _state.Peek == PeekKind.Volume) ||
            (!_current.ShowBattery && _state.Peek == PeekKind.Battery) ||
            (!_current.ShowMedia && _state.Peek == PeekKind.Track))
        {
            _state.ClearPeek();
        }

        _window?.View.SetBattery(_current.ShowBattery ? ReadBattery() : null);
        Render();
    }

    private void StopVolume()
    {
        if (_volume is null)
        {
            return;
        }

        _volume.VolumeChanged -= OnVolumeChanged;
        _volume.DefaultDeviceChanged -= OnDefaultAudioDeviceChanged;
        _volume.DevicesChanged -= OnAudioDevicesChanged;
        _volume.Dispose();
        _volume = null;
    }

    private void StopConnectivity()
    {
        if (_connectivity is null)
        {
            return;
        }

        _connectivity.Changed -= OnConnectivityChanged;
        _connectivity.Dispose();
        _connectivity = null;
        _window?.View.SetConnectivity(RadioInfo.Unavailable, RadioInfo.Unavailable, NetworkLink.None);
    }

    private void StopSystem()
    {
        if (_system is null)
        {
            return;
        }

        _system.HardwareChanged -= OnHardwareChanged;
        _system.SnapshotChanged -= OnSystemSnapshotChanged;
        _system.Dispose();
        _system = null;
    }

    /// <summary>
    /// Discos e sensores só são lidos com a página deles na tela e a ilha aberta: a aba "Sistema" amostra a cada
    /// segundo enquanto visível; os discos são relidos cada vez que a aba "Armazenamento" aparece.
    /// </summary>
    private void UpdatePageSources()
    {
        var page = _window is not null && _state.Mode == NotchMode.Expanded ? _window.View.CurrentPage : (NotchPage?)null;
        _system?.SetActive(page == NotchPage.System);

        var storageVisible = page == NotchPage.Storage && _current.ShowStorage;
        if (storageVisible && !_storageVisible)
        {
            _ = RefreshDrivesAsync();
        }

        _storageVisible = storageVisible;
    }

    private async Task RefreshDrivesAsync()
    {
        var scan = ++_driveScan;
        var drives = await DriveScanner.ScanAsync(logger);

        // Volta na thread de UI (contexto de sincronização do DispatcherQueue); descarta leituras velhas.
        if (scan == _driveScan)
        {
            Guard(() => _window?.View.SetDrives(drives));
        }
    }

    /// <summary>Leva o volume atual para o slider (ao ligar e ao trocar de saída, quando não há aviso do Core Audio).</summary>
    private void PushVolume()
    {
        if (_volume?.Current is { } volume)
        {
            _window?.View.SetVolume(volume);
        }
    }

    private void RefreshOutputDevices() =>
        _window?.View.SetOutputDevices(_current.ShowAudioControls ? AudioEndpoints.GetOutputDevices(logger) : []);

    private void StopMedia()
    {
        if (_media is null)
        {
            return;
        }

        _media.Changed -= OnMediaChanged;
        _media.Dispose();
        _media = null;
        _lastTrackKey = null;
        _window?.View.SetMedia(null);
    }

    private void ApplyAppearance()
    {
        _window?.View.SetBackground(_current.AcrylicBackground, NotchAppearance.ToBrushOpacity(_current.BackgroundOpacity));
        _window?.View.SetGlassBorder(_current.GlassBorder);
    }

    /// <summary>Aplica o estado atual à janela: forma, conteúdo e visibilidade.</summary>
    private void Render(bool animate = true)
    {
        if (_window is null)
        {
            return;
        }

        var mode = _state.Mode;
        var compact = _visibility == NotchVisibility.Compact;
        var isPlaying = _media?.Current?.IsPlaying == true;
        _window.View.SetMode(mode, _state.Peek, compact);
        _window.SetShape(NotchLayout.ShapeFor(mode, isPlaying, compact, _window.View.HasTabs), animate);
        UpdatePageSources();
        _window.SetVisible(_visibility != NotchVisibility.Hidden && !_sessionLocked && !_displayOff);

        if (mode == NotchMode.Expanded)
        {
            _window.View.UpdateClock(DateTime.Now);
            if (_expandedTimer?.IsRunning == false)
            {
                _expandedTimer.Start();
            }
        }
        else
        {
            _expandedTimer?.Stop();
        }
    }

    /// <summary>Some com tela cheia; vira alça quando a janela em foco passa por baixo.</summary>
    private void UpdateVisibility()
    {
        if (_window is null)
        {
            return;
        }

        var monitor = GetMonitor();
        ForegroundWindow? foreground = null;
        var hwnd = NativeWindow.GetForegroundWindow();
        if (hwnd != 0 && NativeWindow.TryGetWindowRect(hwnd, out var bounds))
        {
            foreground = new ForegroundWindow(
                bounds,
                NativeWindow.GetClassName(hwnd),
                NativeWindow.IsMinimized(hwnd),
                NativeWindow.IsCloaked(hwnd),
                IsOwnProcess: NativeWindow.GetProcessId(hwnd) == (uint)Environment.ProcessId);
        }

        // Testa contra a maior forma recolhida, para a alça não piscar ao começar/parar uma música.
        var island = NotchLayout.Place(monitor.Bounds, monitor.DpiScale, NotchLayout.WindowSize(NotchLayout.Playing));
        var visibility = OverlapRules.Evaluate(
            NativeWindow.GetUserNotificationState(),
            foreground,
            monitor.Bounds,
            island,
            _current.HideOnFullscreen,
            _current.CompactWhenCovered);

        if (visibility != _visibility)
        {
            logger.LogDebug("Ilha: {Visibility} (janela em foco: {Class} {Bounds})", visibility, foreground?.ClassName, foreground?.Bounds);
            _visibility = visibility;
            Render();
        }
    }

    private void UpdateBattery(bool peek)
    {
        var battery = ReadBattery();
        var pluggedIn = battery?.IsPluggedIn;
        var pluggedChanged = _pluggedIn is not null && pluggedIn != _pluggedIn;
        _pluggedIn = pluggedIn;

        if (!_current.ShowBattery)
        {
            return;
        }

        _window?.View.SetBattery(battery);
        if (peek && pluggedChanged && battery is not null && _state.ShowPeek(PeekKind.Battery))
        {
            RestartPeekTimer();
            Render();
        }
    }

    private void ShowPeek(PeekKind kind)
    {
        if (_state.ShowPeek(kind))
        {
            RestartPeekTimer();
            Render();
        }
    }

    private void RestartPeekTimer()
    {
        _peekTimer?.Stop();
        _peekTimer?.Start();
    }

    private void OnHoverChanged(object? sender, bool hovered) => Guard(() =>
    {
        if (hovered)
        {
            _collapseTimer?.Stop();
            if (!_state.IsHovered)
            {
                _expandTimer?.Start();
            }
        }
        else
        {
            _expandTimer?.Stop();
            if (_state.IsHovered)
            {
                _collapseTimer?.Start();
            }
        }
    });

    private void OnExpandElapsed() => Guard(() =>
    {
        _peekTimer?.Stop();
        if (_state.SetHovered(true))
        {
            // Um adaptador Wi‑Fi/Bluetooth USB pode ter sido conectado desde a última vez.
            _ = _connectivity?.RefreshAsync();
            Render();
        }
    });

    private void OnCollapseElapsed() => Guard(() =>
    {
        // Um PointerExited espúrio (clique num botão, por exemplo) não fecha a ilha com o mouse ainda em cima.
        if (_window?.IsCursorInside() == true)
        {
            return;
        }

        if (_state.SetHovered(false))
        {
            Render();
        }
    });

    private void OnPeekElapsed() => Guard(() =>
    {
        if (_state.EndPeek(_state.Peek))
        {
            Render();
        }
    });

    private void OnExpandedTick() => Guard(() =>
    {
        if (_window is null || !_state.IsHovered)
        {
            return;
        }

        _window.View.UpdateClock(DateTime.Now);
        if (!_window.IsCursorInside() && _collapseTimer?.IsRunning == false)
        {
            _collapseTimer.Start();
        }
    });

    private void OnMediaChanged(object? sender, EventArgs e) => Guard(() =>
    {
        var media = _media?.Current;
        _window?.View.SetMedia(media);

        // Faixa nova tocando: avisa (menos na primeira leitura, logo ao ligar o módulo).
        if (media is { IsPlaying: true })
        {
            if (_lastTrackKey is not null && media.TrackKey != _lastTrackKey)
            {
                _state.ShowPeek(PeekKind.Track);
                RestartPeekTimer();
            }

            _lastTrackKey = media.TrackKey;
        }
        else if (media is null)
        {
            _lastTrackKey = null;
        }

        Render();
    });

    private void OnMediaCommandRequested(object? sender, MediaCommand command)
    {
        logger.LogDebug("Comando de mídia: {Command}", command);
        if (_media is not null)
        {
            _ = _media.SendAsync(command);
        }
    }

    private void OnVolumeChanged(object? sender, VolumeLevel volume)
    {
        // Chega numa thread do Core Audio.
        _dispatcher?.TryEnqueue(() => Guard(() =>
        {
            if (_window is null)
            {
                return;
            }

            _window.View.SetVolume(volume);
            if (!_current.ShowVolume)
            {
                return;
            }

            if (_state.Peek == PeekKind.Volume)
            {
                RestartPeekTimer();
            }
            else
            {
                ShowPeek(PeekKind.Volume);
            }
        }));
    }

    private void OnDefaultAudioDeviceChanged(object? sender, EventArgs e)
    {
        // Chega numa thread do Core Audio.
        _dispatcher?.TryEnqueue(() => Guard(() =>
        {
            _volume?.Refresh();
            PushVolume();
            RefreshOutputDevices();
        }));
    }

    private void OnAudioDevicesChanged(object? sender, EventArgs e)
    {
        // Chega numa thread do Core Audio (às vezes várias vezes seguidas para o mesmo fone).
        _dispatcher?.TryEnqueue(() => Guard(RefreshOutputDevices));
    }

    private void OnConnectivityChanged(object? sender, EventArgs e) => Guard(() =>
    {
        if (_connectivity is { } connectivity)
        {
            _window?.View.SetConnectivity(connectivity.WiFi, connectivity.Bluetooth, connectivity.Link);
        }
    });

    private void OnPageChanged(object? sender, NotchPage page) => Guard(UpdatePageSources);

    private void OnHardwareChanged(object? sender, HardwareInfo hardware) => Guard(() => _window?.View.SetHardware(hardware));

    private void OnSystemSnapshotChanged(object? sender, SystemSnapshot snapshot) => Guard(() => _window?.View.SetSystemSnapshot(snapshot));

    /// <summary>Abre o disco no Explorer. A raiz vem da nossa lista de discos; mesmo assim, só aceita uma raiz de unidade.</summary>
    private void OnDriveOpenRequested(object? sender, string root) => Guard(() =>
    {
        if (!Path.IsPathRooted(root) || !string.Equals(Path.GetPathRoot(root), root, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Pedido para abrir algo que não é a raiz de um disco: {Root}", root);
            return;
        }

        // Caminho completo do explorer.exe (não pelo PATH) e o disco como argumento separado, sem shell.
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var start = new System.Diagnostics.ProcessStartInfo(explorer) { UseShellExecute = false };
        start.ArgumentList.Add(root);
        using var process = System.Diagnostics.Process.Start(start);
    });

    private void OnQuickActionRequested(object? sender, QuickAction action) => Guard(() =>
    {
        logger.LogDebug("Controle rápido: {Action}", action);
        switch (action)
        {
            case QuickAction.ToggleWiFi:
                _ = _connectivity?.ToggleAsync(QuickRadio.WiFi);
                break;
            case QuickAction.ToggleBluetooth:
                _ = _connectivity?.ToggleAsync(QuickRadio.Bluetooth);
                break;
            case QuickAction.OpenWiFiList:
                _ = LaunchAsync("ms-availablenetworks:");
                break;
            case QuickAction.OpenBluetoothList:
                _ = LaunchAsync("ms-settings:bluetooth");
                break;
            case QuickAction.OpenEthernetSettings:
                _ = LaunchAsync("ms-settings:network-ethernet");
                break;
            case QuickAction.ToggleMute when _volume?.Current is { } volume:
                _volume.SetMuted(!volume.IsMuted);
                break;
        }
    });

    private void OnVolumeSliderMoved(object? sender, double percent) => Guard(() =>
    {
        if (_volume?.Current is not { } current)
        {
            return;
        }

        var target = QuickControls.FromSlider(current, percent);
        if (target.IsMuted != current.IsMuted)
        {
            _volume.SetMuted(target.IsMuted);
        }

        _volume.SetLevel(target.Level);
    });

    private void OnOutputDeviceSelected(object? sender, AudioOutputDevice device) => Guard(() =>
    {
        if (device.IsDefault)
        {
            return;
        }

        logger.LogInformation("Trocando a saída de áudio para {Device}", device.Name);
        if (!AudioEndpoints.TrySetDefaultOutput(device.Id, logger))
        {
            // Falhou (IPolicyConfig mudou numa atualização, por exemplo): a lista volta a mostrar a saída real.
            RefreshOutputDevices();
        }

        // Deu certo: o aviso de troca de dispositivo padrão atualiza o volume e a lista.
    });

    /// <summary>Abre um painel do Windows (URIs fixas do sistema). Não lança.</summary>
    private async Task LaunchAsync(string uri)
    {
        try
        {
            if (!await Windows.System.Launcher.LaunchUriAsync(new Uri(uri)))
            {
                logger.LogDebug("O Windows não abriu {Uri}", uri);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Falha ao abrir {Uri}", uri);
        }
    }

    private void OnPowerChanged(object? sender, object e)
    {
        // Chega numa thread qualquer.
        _dispatcher?.TryEnqueue(() => Guard(() => UpdateBattery(peek: true)));
    }

    private void OnSettingsChanged(object? sender, NotchSettings e) => Guard(() =>
    {
        _current = e;
        ApplyAppearance();
        ApplySources();
        UpdateVisibility();
    });

    private void OnSessionLockChanged(object? sender, bool locked) => Guard(() =>
    {
        _sessionLocked = locked;
        Render(animate: false);
    });

    private void OnDisplayPowerChanged(object? sender, bool on) => Guard(() =>
    {
        _displayOff = !on;
        Render(animate: false);
    });

    private void OnDisplaysChanged(object? sender, EventArgs e) => Guard(() =>
    {
        _window?.SetMonitor(GetMonitor());
        UpdateVisibility();
    });

    private void OnWinEvent(object? sender, WinEventArgs e)
    {
        // Rajadas de eventos (arrastar uma janela gera dezenas): reavalia uma vez, um pouco depois.
        if (e.Kind is WinEventKind.ForegroundChanged or WinEventKind.LocationChanged or WinEventKind.MinimizeEnded &&
            _overlapTimer is not null)
        {
            _overlapTimer.Stop();
            _overlapTimer.Start();
        }
    }

    private DisplayMonitor GetMonitor()
    {
        if (NativeWindow.TryGetPrimaryMonitor(out var monitor))
        {
            return monitor;
        }

        logger.LogWarning("Monitor principal não encontrado; usando 1920x1080");
        var fallback = PixelRect.FromSize(0, 0, 1920, 1080);
        return new DisplayMonitor(fallback, fallback, 1.0);
    }

    /// <summary>Bateria atual, ou <c>null</c> em PCs sem bateria.</summary>
    private static BatteryInfo? ReadBattery() =>
        PowerManager.BatteryStatus == BatteryStatus.NotPresent
            ? null
            : new BatteryInfo(PowerManager.RemainingChargePercent, PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent);

    private DispatcherQueueTimer CreateTimer(TimeSpan interval, bool repeating, Action tick)
    {
        var timer = _dispatcher!.CreateTimer();
        timer.Interval = interval;
        timer.IsRepeating = repeating;
        timer.Tick += (_, _) => tick();
        return timer;
    }

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
