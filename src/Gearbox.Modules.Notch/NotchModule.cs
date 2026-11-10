using Gearbox.Core.Modules;
using Gearbox.Core.Settings;
using Gearbox.Interop.Audio;
using Gearbox.Interop.Desktop;
using Gearbox.Interop.Geometry;
using Gearbox.Interop.Session;
using Gearbox.Interop.Windowing;
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
    ISettingsService<MixerSettings> mixerSettings,
    IModuleFaultReporter faults,
    IModuleSettingsOpener settingsOpener,
    LinkCatalog links,
    LinkEditor linkEditor,
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

    private static readonly TimeSpan ContrastTick = TimeSpan.FromSeconds(1);

    private readonly NotchState _state = new();
    private NotchSettings _current = new();
    private DispatcherQueue? _dispatcher;
    private NotchWindow? _window;
    private SystemStateMonitor? _systemState;
    private AudioVolumeMonitor? _volume;
    private MediaSessionWatcher? _media;
    private ConnectivityWatcher? _connectivity;
    private SystemMonitor? _system;
    private ClipboardWatcher? _clipboard;
    private AudioMixer? _mixer;
    private NetworkMonitor? _network;
    private readonly NetworkHistory _networkHistory = new();
    private bool _networkVisible;
    private SpeedTester? _speedTester;
    private CancellationTokenSource? _speedTest;
    private bool _meteredConfirmShown;
    private AccentColorSource? _accent;
    private bool _loggedUnsupportedBlur;
    private IDisposable? _subscription;
    private bool _storageVisible;
    private int _driveScan;
    private DispatcherQueueTimer? _expandTimer;
    private DispatcherQueueTimer? _collapseTimer;
    private DispatcherQueueTimer? _peekTimer;
    private DispatcherQueueTimer? _overlapTimer;
    private DispatcherQueueTimer? _expandedTimer;
    private DispatcherQueueTimer? _contrastTimer;
    private TintColor _tint = NotchAppearance.Black;
    private TextTone _tone;
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
            _contrastTimer = CreateTimer(ContrastTick, repeating: true, () => Guard(UpdateContrast));

            _current = settings.Load();
            _window = new NotchWindow(GetMonitor(), logger);
            _window.View.HoverChanged += OnHoverChanged;
            _window.View.MediaCommandRequested += OnMediaCommandRequested;
            _window.View.QuickActionRequested += OnQuickActionRequested;
            _window.View.VolumeSliderMoved += OnVolumeSliderMoved;
            _window.View.OutputDeviceSelected += OnOutputDeviceSelected;
            _window.View.PageChanged += OnPageChanged;
            _window.View.DriveOpenRequested += OnDriveOpenRequested;
            _window.View.ClipboardCopyRequested += OnClipboardCopyRequested;
            _window.View.ClipboardRemoveRequested += OnClipboardRemoveRequested;
            _window.View.ClipboardClearRequested += OnClipboardClearRequested;
            _window.View.SettingsRequested += OnSettingsRequested;
            _window.View.LinkOpenRequested += OnLinkOpenRequested;
            _window.View.LinkAddRequested += OnLinkAddRequested;
            _window.View.MixerVolumeChanged += OnMixerVolumeChanged;
            _window.View.MixerMuteRequested += OnMixerMuteRequested;
            _window.View.MixerOutputSelected += OnMixerOutputSelected;
            _window.View.SpeedTestRequested += OnSpeedTestRequested;
            links.Changed += OnLinksChanged;
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

        foreach (var timer in new[] { _expandTimer, _collapseTimer, _peekTimer, _overlapTimer, _expandedTimer, _contrastTimer })
        {
            timer?.Stop();
        }

        _tone = TextTone.Light;

        if (_systemState is not null)
        {
            _systemState.SessionLockChanged -= OnSessionLockChanged;
            _systemState.DisplayPowerChanged -= OnDisplayPowerChanged;
            _systemState.DisplaysChanged -= OnDisplaysChanged;
            _systemState.Dispose();
            _systemState = null;
        }

        StopMixer();
        StopNetwork();
        StopVolume();
        StopMedia();
        StopConnectivity();
        StopSystem();
        StopClipboard();
        StopAccent();
        _loggedUnsupportedBlur = false;
        links.Changed -= OnLinksChanged;
        linkEditor.Close();

        if (_window is not null)
        {
            _window.View.PageChanged -= OnPageChanged;
            _window.View.DriveOpenRequested -= OnDriveOpenRequested;
            _window.View.ClipboardCopyRequested -= OnClipboardCopyRequested;
            _window.View.ClipboardRemoveRequested -= OnClipboardRemoveRequested;
            _window.View.ClipboardClearRequested -= OnClipboardClearRequested;
            _window.View.SettingsRequested -= OnSettingsRequested;
            _window.View.LinkOpenRequested -= OnLinkOpenRequested;
            _window.View.LinkAddRequested -= OnLinkAddRequested;
            _window.View.MixerVolumeChanged -= OnMixerVolumeChanged;
            _window.View.MixerMuteRequested -= OnMixerMuteRequested;
            _window.View.MixerOutputSelected -= OnMixerOutputSelected;
            _window.View.SpeedTestRequested -= OnSpeedTestRequested;
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
        var needsVolume = _current.ShowVolume || _current.ShowAudioControls || _current.ShowMixer;
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

        if (_current.ShowConnectivity && _network is null && _dispatcher is not null)
        {
            _network = new NetworkMonitor(_dispatcher, logger);
            _network.Sampled += OnNetworkSampled;
        }
        else if (!_current.ShowConnectivity)
        {
            StopNetwork();
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

        if (_current.ShowClipboard && _clipboard is null)
        {
            _clipboard = new ClipboardWatcher(logger);
            _clipboard.Changed += OnClipboardChanged;
            PushClipboard();
        }
        else if (!_current.ShowClipboard)
        {
            StopClipboard();
        }

        if (_current.ShowMixer && _mixer is null && _dispatcher is not null)
        {
            StartMixer(_dispatcher);
        }
        else if (!_current.ShowMixer)
        {
            StopMixer();
        }

        _window?.View.SetMixerEnabled(_mixer is not null, AppAudioRouting.IsSupported);
        _window?.View.SetTabs(_current.ShowConnectivity, _current.ShowAudioControls, _current.ShowStorage, _current.ShowSystemInfo, _current.ShowClipboard, _current.ShowLinks);
        if (_current.ShowLinks)
        {
            PushLinks();
            links.EnsureIcons();
        }
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

    private void StartMixer(DispatcherQueue dispatcher)
    {
        try
        {
            _mixer = new AudioMixer(dispatcher, mixerSettings, logger, Guard);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException)
        {
            logger.LogWarning(ex, "Não foi possível acompanhar o áudio dos apps");
            return;
        }

        _mixer.AppsChanged += OnMixerAppsChanged;
        _mixer.PeaksChanged += OnMixerPeaksChanged;
        _window?.View.SetMixerApps(_mixer.Apps);
    }

    private void StopMixer()
    {
        if (_mixer is null)
        {
            return;
        }

        _mixer.AppsChanged -= OnMixerAppsChanged;
        _mixer.PeaksChanged -= OnMixerPeaksChanged;
        var mixer = _mixer;
        _mixer = null;
        _window?.View.SetMixerEnabled(false);
        mixer.Dispose();
    }

    private void OnMixerAppsChanged(object? sender, EventArgs e) => Guard(() =>
    {
        if (_mixer is not null)
        {
            _window?.View.SetMixerApps(_mixer.Apps);
        }
    });

    private void OnMixerPeaksChanged(object? sender, EventArgs e) => Guard(() =>
    {
        if (_mixer is not null)
        {
            _window?.View.SetMixerPeaks(_mixer.Peaks);
        }
    });

    private void OnMixerVolumeChanged(object? sender, MixerVolumeRequest request) =>
        Guard(() => _mixer?.SetVolume(request.Key, request.Percent));

    private void OnMixerMuteRequested(object? sender, string key) => Guard(() => _mixer?.ToggleMute(key));

    private void OnMixerOutputSelected(object? sender, MixerOutputRequest request) => Guard(() =>
    {
        logger.LogInformation("Trocando a saída de áudio de {App}", request.Key);
        _mixer?.SetOutput(request.Key, request.DeviceId);
    });

    private void StopNetwork()
    {
        _speedTest?.Cancel();
        _speedTest = null;
        _speedTester?.Dispose();
        _speedTester = null;
        _window?.View.SetSpeedTestRunning(false);
        _window?.View.SetSpeedTestMessage("SpeedTestHint");
        _networkVisible = false;
        if (_network is null)
        {
            return;
        }

        _network.Sampled -= OnNetworkSampled;
        _network.Dispose();
        _network = null;
    }

    private void OnNetworkSampled(object? sender, NetworkSample sample) => Guard(() =>
    {
        _networkHistory.Add(sample);
        _window?.View.SetNetworkHistory(_networkHistory);
    });

    private async void OnSpeedTestRequested(object? sender, EventArgs e)
    {
        if (!IsEnabled || _window is null)
        {
            return;
        }

        switch (SpeedTestGate.Decide(_speedTest is not null, ConnectionCost.IsMetered(logger), _meteredConfirmShown))
        {
            case SpeedTestDecision.Cancel:
                _speedTest?.Cancel();
                return;
            case SpeedTestDecision.ConfirmMetered:
                _meteredConfirmShown = true;
                Guard(() => _window?.View.SetSpeedTestMessage("SpeedTestMeteredConfirm"));
                return;
        }

        _meteredConfirmShown = false;

        var cancellation = new CancellationTokenSource();
        var view = _window.View;
        SpeedTester? tester = null;
        Progress<SpeedTestProgress>? progress = null;
        Guard(() =>
        {
            tester = _speedTester ??= new SpeedTester();
            _speedTest = cancellation;
            view.SetSpeedTestRunning(true);
            progress = new Progress<SpeedTestProgress>(update => Guard(() =>
            {
                if (_speedTest == cancellation)
                {
                    view.SetSpeedTestProgress(update);
                }
            }));
        });

        if (tester is null || progress is null)
        {
            cancellation.Dispose();
            return;
        }

        string? message = null;
        try
        {
            var result = await tester.RunAsync(progress, cancellation.Token);
            logger.LogInformation(
                "Teste de velocidade: ping {Ping} ms, download {Down} bit/s, upload {Up} bit/s",
                result.PingMilliseconds,
                result.DownBitsPerSecond,
                result.UpBitsPerSecond);
        }
        catch (Exception ex) when (cancellation.IsCancellationRequested && ex is OperationCanceledException or ObjectDisposedException or HttpRequestException or IOException)
        {
            message = "SpeedTestCanceled";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Teste de velocidade falhou");
            message = "SpeedTestFailed";
        }
        finally
        {
            cancellation.Dispose();
        }

        Guard(() =>
        {
            if (_speedTest != cancellation)
            {
                return;
            }

            _speedTest = null;
            view.SetSpeedTestRunning(false);
            if (message is not null)
            {
                view.SetSpeedTestMessage(message);
            }
        });
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

    private void StopClipboard()
    {
        if (_clipboard is null)
        {
            return;
        }

        _clipboard.Changed -= OnClipboardChanged;
        _clipboard.Dispose();
        _clipboard = null;
        PushClipboard();
    }

    private void PushClipboard() =>
        _window?.View.SetClipboard(
            _clipboard?.Items ?? [],
            id => _clipboard?.OpenImage(id),
            (id, ex) => logger.LogWarning(ex, "Não foi possível mostrar a miniatura do item {Id} da área de transferência", id));

    private void OnClipboardChanged(object? sender, EventArgs e) => Guard(PushClipboard);

    private async void OnClipboardCopyRequested(object? sender, long id)
    {
        if (_clipboard is not { } clipboard)
        {
            return;
        }

        var copied = await clipboard.CopyBackAsync(id);
        if (copied)
        {
            Guard(() => _window?.View.ShowClipboardCopied());
        }
    }

    private void OnClipboardRemoveRequested(object? sender, long id) => Guard(() => _clipboard?.Remove(id));

    private void OnClipboardClearRequested(object? sender, EventArgs e) => Guard(() => _clipboard?.Clear());

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
        _mixer?.SetMeterActive(page == NotchPage.Mixer);

        var networkVisible = page == NotchPage.Network && _network is not null;
        if (networkVisible && !_networkVisible)
        {
            _networkHistory.Clear();
            _window?.View.SetNetworkHistory(_networkHistory);
        }

        if (!networkVisible)
        {
            _meteredConfirmShown = false;
        }

        _networkVisible = networkVisible;
        _network?.SetActive(networkVisible);

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
        _window?.View.SetOutputDevices(_current.ShowAudioControls || _current.ShowMixer ? AudioEndpoints.GetOutputDevices(logger) : []);

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
        if (_window is null)
        {
            return;
        }

        var look = NotchAppearance.Look(_current.BackgroundStyle);
        if (look.Blur && !_loggedUnsupportedBlur && !IslandBackdrop.IsSupported())
        {
            _loggedUnsupportedBlur = true;
            logger.LogInformation("O acrílico não é suportado neste sistema; o estilo Vidro usa só a cor");
        }

        _tint = NotchAppearance.Readable(ResolveColor());
        _window.View.SetBackground(look.Blur, _tint, look.Opacity);
        _window.View.SetGlassBorder(_current.GlassBorder);
        _window.View.SetAnimation(_current.SpringAnimation, _current.SpringIntensity);
        UpdateContrast();
    }

    private void UpdateContrast()
    {
        if (_window is null)
        {
            return;
        }

        var look = NotchAppearance.Look(_current.BackgroundStyle);
        if (!_current.AdaptTextContrast || (!look.Blur && look.Opacity >= 1))
        {
            _contrastTimer?.Stop();
            SetTextTone(TextTone.Light);
            return;
        }

        if (_contrastTimer?.IsRunning == false)
        {
            _contrastTimer.Start();
        }

        if (!_window.IsVisible)
        {
            return;
        }

        var monitor = GetMonitor();
        var captures = TextContrast.SampleAreas(_window.Bounds, monitor.Bounds, monitor.DpiScale)
            .Select(ScreenPixels.CaptureBgra)
            .OfType<byte[]>();
        if (TextContrast.Average(captures) is not { } behind)
        {
            return;
        }

        var luminance = TextContrast.Luminance(TextContrast.Blend(_tint, look.Opacity, behind));
        SetTextTone(TextContrast.Choose(luminance, _tone));
    }

    private void SetTextTone(TextTone tone)
    {
        if (tone == _tone || _window is null)
        {
            return;
        }

        logger.LogDebug("Ilha: texto {Tone}", tone);
        _tone = tone;
        _window.View.SetTextTone(tone);
    }

    private TintColor ResolveColor()
    {
        if (!_current.UseAccentColor)
        {
            StopAccent();
            return NotchAppearance.ParseOrDefault(_current.BackgroundColor);
        }

        if (_accent is null && _dispatcher is not null)
        {
            _accent = new AccentColorSource(_dispatcher);
            _accent.Changed += OnAccentChanged;
        }

        return _accent?.Current ?? NotchAppearance.Black;
    }

    private void StopAccent()
    {
        if (_accent is null)
        {
            return;
        }

        _accent.Changed -= OnAccentChanged;
        _accent.Dispose();
        _accent = null;
    }

    private void OnAccentChanged(object? sender, EventArgs e) => Guard(ApplyAppearance);

    private void PushLinks() => _window?.View.SetLinks(links.Links, links.IconPath);

    private void OnLinksChanged(object? sender, EventArgs e) => Guard(PushLinks);

    private void OnLinkOpenRequested(object? sender, string id) => Guard(() =>
    {
        if (links.Links.FirstOrDefault(l => l.Id == id) is not { } link)
        {
            return;
        }

        CollapseNow();
        try
        {
            LinkLauncher.Open(link);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(ex, "Não foi possível abrir o link {Target}", link.Target);
        }
    });

    private void OnLinkAddRequested(object? sender, EventArgs e) => Guard(() =>
    {
        CollapseNow();
        linkEditor.Open();
    });

    private void CollapseNow()
    {
        _expandTimer?.Stop();
        _collapseTimer?.Stop();
        if (_state.SetHovered(false))
        {
            Render();
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e) => Guard(() =>
    {
        CollapseNow();
        settingsOpener.Open(Id);
    });

    /// <summary>Aplica o estado atual à janela: forma, conteúdo e visibilidade.</summary>
    private void Render(bool animate = true)
    {
        if (_window is null)
        {
            return;
        }

        var mode = _state.Mode;
        var showsMedia = NotchLayout.ShowsCollapsedMedia(_media?.Current?.IsPlaying == true, _current.ShowMediaWhenCollapsed);
        var compact = NotchLayout.IsCompact(_visibility, _current.CollapseToHandle, showsMedia);
        _window.View.SetMode(mode, _state.Peek, compact, showsMedia);
        var tall = _window.View.CurrentPage is NotchPage.Clipboard or NotchPage.Links || _window.View.ShowsMixer || _window.View.ShowsNetwork;
        var shape = NotchLayout.ShapeFor(mode, showsMedia, compact, _window.View.HasTabs, tall, _window.View.ShowsFullControls);
        _window.SetShape(NotchLayout.WithRoundness(shape, NotchAppearance.RoundnessFactor(_current.Roundness)), animate);
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

        UpdateContrast();
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
        if (_window?.View.IsPointerOverCollapsedControls == true)
        {
            return;
        }

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

    private void OnPageChanged(object? sender, NotchPage page) => Guard(() => Render());

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
                _volume.TrySetMuted(!volume.IsMuted);
                break;
        }
    });

    private void OnVolumeSliderMoved(object? sender, double percent) => Guard(() =>
    {
        if (_volume?.Current is not { } current)
        {
            return;
        }

        if (!QuickControls.TryApplySlider(current, percent, _volume.TrySetLevel, _volume.TrySetMuted))
        {
            PushVolume();
        }
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

