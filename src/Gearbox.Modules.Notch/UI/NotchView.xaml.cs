using System.Runtime.InteropServices.WindowsRuntime;
using System.Globalization;
using Gearbox.Interop.Audio;
using Gearbox.Modules.Notch.Logic;
using Gearbox.Modules.Notch.Native;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Gearbox.Modules.Notch.UI;

/// <summary>Conteúdo da ilha: a forma preta animada e o que aparece dentro dela em cada modo.</summary>
public sealed partial class NotchView : UserControl
{
    private static readonly Duration ShapeDuration = new(TimeSpan.FromMilliseconds(340));
    private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(180));
    private static readonly Duration FadeOutDuration = new(TimeSpan.FromMilliseconds(100));

    private const double ContentStartScale = 0.96;
    private const double ContentStartOffset = -6;

    private readonly Storyboard _bars;
    private readonly SpringAnimator _shapeSpring = new();
    private readonly SpringAnimator _contentSpring = new();
    private Storyboard? _shape;
    private bool _springEnabled = true;
    private SpringMotion _springMotion = SpringMotion.For(SpringIntensity.Normal);
    private bool? _blur;
    private TintColor _tint;
    private UIElement? _visiblePanel;
    private MediaSnapshot? _media;
    private BatteryInfo? _battery;
    private PeekKind _peek;
    private string _volumeGlyph = NotchGlyphs.Mute;
    private int _artworkVersion = -1;
    private int _artworkLoad;
    private bool _barsRunning;
    private bool _showsCollapsedMedia;
    private bool _pointerOverCollapsedControls;
    private bool _hasConnectivity;
    private bool _hasAudioControls;
    private bool _hasStorage;
    private bool _hasSystem;
    private bool _hasClipboard;
    private bool _hasLinks;
    private readonly Dictionary<string, (DateTime Stamp, ImageSource Image)> _linkIcons = [];
    private readonly Dictionary<long, ClipboardEntry> _clipboardEntries = [];
    private DispatcherQueueTimer? _clipboardCopiedTimer;
    private RadioInfo _wifi = RadioInfo.Unavailable;
    private RadioInfo _bluetooth = RadioInfo.Unavailable;
    private NetworkLink _link = NetworkLink.None;
    private NotchPage _page = NotchPage.Media;
    private ulong _gpuTotalBytes;
    private bool _updatingVolume;

    public NotchView()
    {
        InitializeComponent();
        _bars = CreateBarsAnimation();
        _visiblePanel = CollapsedPanel;
        ArtPlaceholder.Glyph = NotchGlyphs.MusicNote;
        PlayPauseIcon.Glyph = NotchGlyphs.Play;
        MuteIcon.Glyph = _volumeGlyph;
        SetMedia(null);
        SetConnectivity(_wifi, _bluetooth, _link);
        SetOutputDevices([]);
        SetDrives([]);
        SetHardware(new HardwareInfo(null, null, null));
    }

    /// <summary>O mouse entrou (<c>true</c>) ou saiu (<c>false</c>) da ilha.</summary>
    public event EventHandler<bool>? HoverChanged;

    public event EventHandler<MediaCommand>? MediaCommandRequested;

    public event EventHandler<QuickAction>? QuickActionRequested;

    /// <summary>O usuário moveu o slider de volume (0–100).</summary>
    public event EventHandler<double>? VolumeSliderMoved;

    public event EventHandler<AudioOutputDevice>? OutputDeviceSelected;

    /// <summary>A página visível mudou (o módulo liga/desliga as leituras de discos e sensores).</summary>
    public event EventHandler<NotchPage>? PageChanged;

    /// <summary>Clique num disco da aba "Armazenamento": a raiz (<c>C:\</c>) a abrir no Explorer.</summary>
    public event EventHandler<string>? DriveOpenRequested;

    public event EventHandler<long>? ClipboardCopyRequested;

    public event EventHandler<long>? ClipboardRemoveRequested;

    public event EventHandler? ClipboardClearRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler<string>? LinkOpenRequested;

    public event EventHandler? LinkAddRequested;

    public event EventHandler<NotchShape>? ShapeFrame;

    /// <summary>A ilha aberta mostra as abas (alguma aba além de "Mídia" ligada): usa a forma mais alta.</summary>
    public bool HasTabs => _hasConnectivity || _hasAudioControls || _hasStorage || _hasSystem || _hasClipboard || _hasLinks;

    public NotchPage CurrentPage => _page;

    /// <summary>Tamanho atual da forma (durante uma animação, o do quadro atual).</summary>
    public DipSize CurrentSize => new(AppliedLength(Island.Width, Island.ActualWidth), AppliedLength(Island.Height, Island.ActualHeight));

    /// <summary>Raio atual das orelhas (durante uma animação, o do quadro atual).</summary>
    public double CurrentEarRadius => AppliedLength(LeftEar.Width, LeftEar.ActualWidth);

    private static double AppliedLength(double length, double actual) => double.IsNaN(length) ? actual : length;

    /// <summary>Tamanho que a janela precisa agora: o corpo mais as duas orelhas.</summary>
    public DipSize CurrentWindowSize => NotchLayout.WindowSize(CurrentSize, CurrentEarRadius);

    public bool IsAnimatingShape => _shape is not null || _shapeSpring.IsRunning;

    public bool IsPointerOverCollapsedControls =>
        _pointerOverCollapsedControls && _visiblePanel == CollapsedPanel && CollapsedControls.Visibility == Visibility.Visible;

    public void SetShape(NotchShape shape, bool animate, Action? completed = null)
    {
        var from = CurrentSize;
        var fromEar = CurrentEarRadius;
        StopShapeAnimation();

        var corners = new CornerRadius(0, 0, shape.CornerRadius, shape.CornerRadius);
        Island.CornerRadius = corners;
        Backdrop.CornerRadius = corners;
        Tint.CornerRadius = corners;
        GlassEdge.CornerRadius = corners;
        GlassEdge.Margin = new Thickness(0, shape.EarRadius, 0, 0);
        if (!animate)
        {
            ApplySize(shape);
            completed?.Invoke();
            return;
        }

        Island.Width = from.Width;
        Island.Height = from.Height;
        SetEarRadius(fromEar);
        SetEarsVisible(fromEar > 0 || shape.EarRadius > 0);

        if (_springEnabled)
        {
            _shapeSpring.Start(
                _springMotion,
                [
                    new SpringChannel(from.Width, shape.Size.Width, value => Island.Width = Math.Max(0, value)),
                    new SpringChannel(from.Height, shape.Size.Height, value => Island.Height = Math.Max(0, value)),
                    new SpringChannel(fromEar, shape.EarRadius, value => SetEarRadius(Math.Max(0, value))),
                ],
                RaiseSpringFrame,
                () =>
                {
                    ApplySize(shape);
                    RaiseSpringFrame();
                    completed?.Invoke();
                });
            return;
        }

        var storyboard = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        storyboard.Children.Add(CreateAnimation(Island, "Width", shape.Size.Width, ease));
        storyboard.Children.Add(CreateAnimation(Island, "Height", shape.Size.Height, ease));
        foreach (var ear in new FrameworkElement[] { LeftEar, RightEar })
        {
            storyboard.Children.Add(CreateAnimation(ear, "Width", shape.EarRadius, ease));
            storyboard.Children.Add(CreateAnimation(ear, "Height", shape.EarRadius, ease));
        }

        storyboard.Completed += (_, _) =>
        {
            if (_shape != storyboard)
            {
                return;
            }

            StopShapeAnimation();
            ApplySize(shape);
            completed?.Invoke();
        };

        _shape = storyboard;
        CompositionTarget.Rendering += OnStoryboardFrame;
        storyboard.Begin();
    }

    public void SetAnimation(bool spring, SpringIntensity intensity)
    {
        _springEnabled = spring;
        _springMotion = SpringMotion.For(intensity);
        if (spring)
        {
            return;
        }

        _contentSpring.Stop();
        foreach (var panel in new UIElement[] { CollapsedPanel, PeekPanel, ExpandedPanel })
        {
            if (panel.RenderTransform is CompositeTransform transform)
            {
                transform.ScaleX = transform.ScaleY = 1;
                transform.TranslateY = 0;
            }
        }
    }

    public void StopAnimations()
    {
        StopShapeAnimation();
        _contentSpring.Stop();
    }

    private void StopShapeAnimation()
    {
        CompositionTarget.Rendering -= OnStoryboardFrame;
        _shape?.Stop();
        _shape = null;
        _shapeSpring.Stop();
    }

    private void OnStoryboardFrame(object? sender, object e) =>
        ShapeFrame?.Invoke(this, new NotchShape(CurrentSize, 0, CurrentEarRadius));

    private void RaiseSpringFrame() =>
        ShapeFrame?.Invoke(this, new NotchShape(new DipSize(Island.Width, Island.Height), 0, LeftEar.Width));

    private void SetEarRadius(double radius) =>
        LeftEar.Width = LeftEar.Height = RightEar.Width = RightEar.Height = radius;

    private void ApplySize(NotchShape shape)
    {
        Island.Width = shape.Size.Width;
        Island.Height = shape.Size.Height;
        SetEarRadius(shape.EarRadius);
        SetEarsVisible(shape.EarRadius > 0);
    }

    /// <summary>Com raio 0, escondidas: um Path de tamanho 0 ainda pode desenhar um ponto do traço.</summary>
    private void SetEarsVisible(bool visible) =>
        LeftEar.Visibility = RightEar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Troca o conteúdo da ilha para o modo atual.</summary>
    public void SetMode(NotchMode mode, PeekKind peek, bool compact, bool showsCollapsedMedia)
    {
        _showsCollapsedMedia = showsCollapsedMedia;
        UpdateCollapsedMedia();

        UIElement? panel = mode switch
        {
            NotchMode.Expanded => ExpandedPanel,
            NotchMode.Peek => PeekPanel,
            _ when compact => null, // Alça: sem conteúdo.
            _ => CollapsedPanel,
        };

        if (mode == NotchMode.Peek)
        {
            ShowPeekContent(peek);
        }

        // Fechou com a lista de saídas aberta: na próxima vez, volta para os controles.
        if (mode != NotchMode.Expanded && DevicesPage.Visibility == Visibility.Visible)
        {
            ShowPage(NotchPage.Controls);
        }

        UpdateBars(panel == CollapsedPanel && ShowsCollapsedMedia);

        if (panel == _visiblePanel)
        {
            return;
        }

        if (_visiblePanel is not null)
        {
            FadeOut(_visiblePanel);
        }

        _visiblePanel = panel;
        if (panel is not null)
        {
            panel.Opacity = 0;
            panel.IsHitTestVisible = true;
            panel.Visibility = Visibility.Visible;
            var fade = new Storyboard();
            var animation = CreateAnimation(panel, "Opacity", 1, null, FadeDuration);
            animation.BeginTime = TimeSpan.FromMilliseconds(90);
            fade.Children.Add(animation);
            fade.Begin();
            if (_springEnabled)
            {
                StartContentSpring(panel);
            }
        }
    }

    private void StartContentSpring(UIElement panel)
    {
        if (panel.RenderTransform is not CompositeTransform transform)
        {
            transform = new CompositeTransform();
            panel.RenderTransform = transform;
            panel.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0);
        }

        _contentSpring.Start(
            _springMotion,
            [
                new SpringChannel(ContentStartScale, 1, value => transform.ScaleX = transform.ScaleY = value),
                new SpringChannel(ContentStartOffset, 0, value => transform.TranslateY = value),
            ]);
    }

    private void FadeOut(UIElement panel)
    {
        panel.IsHitTestVisible = false;
        var fade = new Storyboard();
        fade.Children.Add(CreateAnimation(panel, "Opacity", 0, null, FadeOutDuration));
        fade.Completed += (_, _) =>
        {
            if (panel != _visiblePanel)
            {
                panel.Visibility = Visibility.Collapsed;
            }
        };
        fade.Begin();
    }

    public void SetMedia(MediaSnapshot? media)
    {
        _media = media;
        var hasMedia = media is not null;
        MediaPanel.Visibility = hasMedia ? Visibility.Visible : Visibility.Collapsed;
        ClockPanel.Visibility = hasMedia ? Visibility.Collapsed : Visibility.Visible;

        TitleText.Text = media?.Title ?? string.Empty;
        ArtistText.Text = media?.Artist ?? string.Empty;
        PlayPauseIcon.Glyph = media?.IsPlaying == true ? NotchGlyphs.Pause : NotchGlyphs.Play;
        PreviousButton.IsEnabled = CollapsedPreviousButton.IsEnabled = media?.CanGoPrevious == true;
        NextButton.IsEnabled = CollapsedNextButton.IsEnabled = media?.CanGoNext == true;

        UpdateCollapsedMedia();
        UpdateBars(ShowsCollapsedMedia && _visiblePanel == CollapsedPanel);

        if (media is null)
        {
            _artworkVersion = -1;
            SetArtwork(null);
        }
        else if (media.ArtworkVersion != _artworkVersion)
        {
            _artworkVersion = media.ArtworkVersion;
            _ = LoadArtworkAsync(media);
        }
    }

    public void SetVolume(VolumeLevel volume)
    {
        var percent = NotchGlyphs.Percent(volume.Level);
        PeekBarScale.ScaleX = volume.IsMuted ? 0 : percent / 100.0;
        PeekValue.Text = volume.IsMuted ? ModuleStrings.Get("VolumeMuted") : ModuleStrings.Format("PercentFormat", percent);
        _volumeGlyph = NotchGlyphs.Volume(volume.Level, volume.IsMuted);
        if (_visiblePanel == PeekPanel && _peek == PeekKind.Volume)
        {
            PeekIcon.Glyph = _volumeGlyph;
        }

        // O slider mostra o nível mesmo no mudo (como o do Windows); o ícone e o texto indicam o mudo.
        MuteIcon.Glyph = _volumeGlyph;
        VolumeText.Text = PeekValue.Text;
        _updatingVolume = true;
        try
        {
            VolumeSlider.Value = percent;
        }
        finally
        {
            _updatingVolume = false;
        }
    }

    /// <summary>
    /// Quais abas e grupos aparecem. "Controles" existe com rádios ou áudio; sem nenhuma aba além de "Mídia", a ilha
    /// aberta fica sem a linha das abas. Se a aba visível sumiu, volta para "Mídia".
    /// </summary>
    public void SetTabs(bool connectivity, bool audio, bool storage, bool system, bool clipboard, bool links)
    {
        _hasLinks = links;
        LinksTab.Visibility = links ? Visibility.Visible : Visibility.Collapsed;
        _hasConnectivity = connectivity;
        _hasAudioControls = audio;
        _hasStorage = storage;
        _hasSystem = system;
        _hasClipboard = clipboard;
        ClipboardTab.Visibility = clipboard ? Visibility.Visible : Visibility.Collapsed;
        RadiosRow.Visibility = connectivity ? Visibility.Visible : Visibility.Collapsed;
        VolumeRow.Visibility = OutputButton.Visibility = audio ? Visibility.Visible : Visibility.Collapsed;
        ControlsTab.Visibility = connectivity || audio ? Visibility.Visible : Visibility.Collapsed;
        StorageTab.Visibility = storage ? Visibility.Visible : Visibility.Collapsed;
        SystemTab.Visibility = system ? Visibility.Visible : Visibility.Collapsed;
        TabsPanel.Visibility = HasTabs ? Visibility.Visible : Visibility.Collapsed;

        var pageGone = _page switch
        {
            NotchPage.Controls => !connectivity && !audio,
            NotchPage.Devices => !audio,
            NotchPage.Storage => !storage,
            NotchPage.System => !system,
            NotchPage.Clipboard => !clipboard,
            NotchPage.Links => !links,
            _ => false,
        };
        if (pageGone)
        {
            ShowPage(_page == NotchPage.Devices && connectivity ? NotchPage.Controls : NotchPage.Media);
        }
    }

    /// <summary>
    /// Botão da rede: com cabo, mostra o cabo, o nome da rede e a velocidade (clique abre as configurações de
    /// Ethernet); senão, o rádio Wi‑Fi como antes.
    /// </summary>
    public void SetConnectivity(RadioInfo wifi, RadioInfo bluetooth, NetworkLink link)
    {
        _wifi = wifi;
        _bluetooth = bluetooth;
        _link = link;

        string tooltip;
        if (link.Kind == NetworkKind.Ethernet)
        {
            var name = string.IsNullOrWhiteSpace(link.Name) ? ModuleStrings.Get("EthernetName") : link.Name;
            var speed = NetworkLinks.FormatSpeed(link.SpeedBitsPerSecond);
            WiFiText.Text = speed.Length == 0 ? name : ModuleStrings.Format("LinkFormat", name, speed);
            WiFiIcon.Glyph = NotchGlyphs.Ethernet;
            WiFiButton.IsEnabled = true;
            WiFiButton.IsChecked = true;
            tooltip = ModuleStrings.Get("EthernetToolTip");
        }
        else
        {
            WiFiText.Text = string.IsNullOrWhiteSpace(wifi.NetworkName) ? ModuleStrings.Get("WiFiName") : wifi.NetworkName;
            WiFiIcon.Glyph = NotchGlyphs.WiFi(wifi.Power);
            WiFiButton.IsEnabled = wifi.Power != RadioPower.Unavailable;
            WiFiButton.IsChecked = wifi.Power == RadioPower.On;
            tooltip = ModuleStrings.Get("WiFiToolTip");
        }

        ToolTipService.SetToolTip(WiFiButton, tooltip);
        AutomationProperties.SetName(WiFiButton, WiFiText.Text);
        BluetoothIcon.Glyph = NotchGlyphs.Bluetooth(bluetooth.Power);
        BluetoothButton.IsEnabled = bluetooth.Power != RadioPower.Unavailable;
        BluetoothButton.IsChecked = bluetooth.Power == RadioPower.On;
    }

    public void SetClipboard(IReadOnlyList<ClipboardItem> items, Func<long, IRandomAccessStream?> openImage, Action<long, Exception> thumbnailFailed)
    {
        var entries = new List<ClipboardEntry>(items.Count);
        foreach (var item in items)
        {
            if (!_clipboardEntries.TryGetValue(item.Id, out var entry))
            {
                entry = ClipboardEntry.Create(item, item.Kind == ClipboardItemKind.Text ? null : openImage(item.Id), thumbnailFailed);
            }

            entries.Add(entry);
        }

        _clipboardEntries.Clear();
        foreach (var entry in entries)
        {
            _clipboardEntries[entry.Id] = entry;
        }

        var media = entries.Where(entry => entry.IsMedia).ToList();
        var texts = entries.Where(entry => !entry.IsMedia).ToList();
        ClipboardMediaList.ItemsSource = media;
        ClipboardMediaList.Visibility = media.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ClipboardTextList.ItemsSource = texts;
        ClipboardTextList.Visibility = texts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoClipboardText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClipboardClearButton.IsEnabled = entries.Count > 0;
    }

    public void ShowClipboardCopied()
    {
        if (_clipboardCopiedTimer is null)
        {
            _clipboardCopiedTimer = DispatcherQueue.CreateTimer();
            _clipboardCopiedTimer.Interval = TimeSpan.FromSeconds(1.5);
            _clipboardCopiedTimer.IsRepeating = false;
            _clipboardCopiedTimer.Tick += (_, _) => ClipboardCopiedText.Visibility = Visibility.Collapsed;
        }

        ClipboardCopiedText.Visibility = Visibility.Visible;
        _clipboardCopiedTimer.Stop();
        _clipboardCopiedTimer.Start();
    }

    public void SetDrives(List<DriveUsage> drives)
    {
        DriveList.ItemsSource = drives.ConvertAll(d => new DriveItem(d));
        NoDrivesText.Visibility = drives.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Modelos (lidos uma vez). Sem placa de vídeo física, a linha da GPU some.</summary>
    public void SetHardware(HardwareInfo hardware)
    {
        CpuName.Text = hardware.CpuName ?? ModuleStrings.Get("CpuUnknown");
        GpuRow.Visibility = hardware.MainGpu is null ? Visibility.Collapsed : Visibility.Visible;
        GpuName.Text = hardware.MainGpu?.Name ?? string.Empty;
        _gpuTotalBytes = hardware.MainGpu?.DedicatedBytes ?? 0;
        BoardName.Text = hardware.BoardName ?? ModuleStrings.Get("BoardUnknown");
        ToolTipService.SetToolTip(BoardName, BoardName.Text);
        ToolTipService.SetToolTip(CpuName, CpuName.Text);
        ToolTipService.SetToolTip(GpuName, GpuName.Text);
    }

    public void SetSystemSnapshot(SystemSnapshot snapshot)
    {
        SetUsage(CpuBarScale, CpuValue, snapshot.CpuPercent, snapshot.ThermalCelsius);
        SetUsage(GpuBarScale, GpuValue, snapshot.GpuPercent, snapshot.GpuCelsius);

        if (snapshot.Memory is { } memory && memory.TotalBytes > 0)
        {
            var percent = memory.UsedBytes * 100.0 / memory.TotalBytes;
            MemoryName.Text = ModuleStrings.Format("MemoryTitleFormat", ByteFormat.Format(memory.UsedBytes), ByteFormat.Format(memory.TotalBytes));
            SetUsage(MemoryBarScale, MemoryValue, percent, null);
        }

        if (snapshot.GpuUsedBytes is { } used && _gpuTotalBytes > 0)
        {
            ToolTipService.SetToolTip(GpuName, ModuleStrings.Format("GpuMemoryToolTipFormat", GpuName.Text, ByteFormat.Format(used), ByteFormat.Format(_gpuTotalBytes)));
        }
    }

    private static void SetUsage(ScaleTransform bar, TextBlock value, double? percent, double? celsius)
    {
        bar.ScaleX = (percent ?? 0) / 100.0;
        var usage = percent is { } p ? ModuleStrings.Format("PercentFormat", (int)Math.Round(p)) : "–";
        value.Text = celsius is { } c ? ModuleStrings.Format("UsageTemperatureFormat", usage, (int)Math.Round(c)) : usage;
    }

    public void SetOutputDevices(List<AudioOutputDevice> devices)
    {
        DeviceList.ItemsSource = devices;
        OutputText.Text = devices.Find(d => d.IsDefault)?.Name ?? ModuleStrings.Get("NoOutputDevice");
        OutputButton.IsEnabled = devices.Count > 0;
    }

    public void SetBattery(BatteryInfo? battery)
    {
        _battery = battery;
        BatteryPanel.Visibility = battery is null ? Visibility.Collapsed : Visibility.Visible;
        if (battery is { } info)
        {
            BatteryIcon.Glyph = info.Glyph;
            BatteryText.Text = ModuleStrings.Format("PercentFormat", info.Percent);
        }

        if (_visiblePanel == PeekPanel && _peek == PeekKind.Battery)
        {
            ShowPeekContent(PeekKind.Battery);
        }
    }

    public void SetBackground(bool blur, TintColor tint, double opacity)
    {
        var island = (SolidColorBrush)Resources["IslandBrush"];
        if (blur != _blur || tint != _tint)
        {
            _blur = blur;
            _tint = tint;
            var color = Windows.UI.Color.FromArgb(255, tint.R, tint.G, tint.B);
            var visible = blur;
            foreach (var element in new[] { Backdrop, LeftEarBackdrop, RightEarBackdrop })
            {
                if (element.SystemBackdrop is not IslandBackdrop backdrop)
                {
                    backdrop = new IslandBackdrop();
                    element.SystemBackdrop = backdrop;
                }

                backdrop.Configure(blur, color);
                element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }

            island.Color = color;
        }

        island.Opacity = opacity;
        ((SolidColorBrush)Resources["ArtPlaceholderBrush"]).Opacity = opacity;
    }

    public void ReleaseBackdrops()
    {
        foreach (var element in new[] { Backdrop, LeftEarBackdrop, RightEarBackdrop })
        {
            element.SystemBackdrop = null;
        }

        _blur = null;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    public void SetGlassBorder(bool visible) =>
        GlassEdge.Visibility = LeftEarEdge.Visibility = RightEarEdge.Visibility =
            visible ? Visibility.Visible : Visibility.Collapsed;

    public void UpdateClock(DateTime now)
    {
        var time = now.ToString("t", CultureInfo.CurrentCulture);
        var date = now.ToString("D", CultureInfo.CurrentCulture);
        ClockText.Text = time;

        // Sem mídia, a hora já aparece grande no lugar dela.
        DateText.Text = _media is null ? date : ModuleStrings.Format("TimeDateFormat", time, date);
    }

    private void ShowPeekContent(PeekKind peek)
    {
        _peek = peek;
        var isVolume = peek == PeekKind.Volume;
        var isTrack = peek == PeekKind.Track;
        PeekBar.Visibility = isVolume ? Visibility.Visible : Visibility.Collapsed;
        PeekText.Visibility = isVolume ? Visibility.Collapsed : Visibility.Visible;
        PeekValue.Visibility = isTrack ? Visibility.Collapsed : Visibility.Visible;
        PeekArtFrame.Visibility = isTrack ? Visibility.Visible : Visibility.Collapsed;
        PeekIcon.Visibility = isTrack ? Visibility.Collapsed : Visibility.Visible;

        switch (peek)
        {
            case PeekKind.Volume:
                PeekIcon.Glyph = _volumeGlyph;
                break;

            case PeekKind.Battery when _battery is { } battery:
                PeekIcon.Glyph = battery.Glyph;
                PeekText.Text = ModuleStrings.Get(battery.Status switch
                {
                    BatteryStatusKind.Charged => "BatteryCharged",
                    BatteryStatusKind.Charging => "BatteryCharging",
                    _ => "BatteryOnBattery",
                });
                PeekValue.Text = ModuleStrings.Format("PercentFormat", battery.Percent);
                break;

            case PeekKind.Track:
                PeekText.Text = _media is null ? string.Empty : string.IsNullOrWhiteSpace(_media.Artist)
                    ? _media.Title
                    : ModuleStrings.Format("TrackFormat", _media.Title, _media.Artist);
                break;
        }
    }

    private async Task LoadArtworkAsync(MediaSnapshot media)
    {
        var load = ++_artworkLoad;
        if (media.Artwork is null)
        {
            SetArtwork(null);
            return;
        }

        try
        {
            using var stream = await media.Artwork.OpenReadAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            if (load == _artworkLoad)
            {
                SetArtwork(bitmap);
            }
        }
        catch (Exception)
        {
            // Capa indisponível (o app fechou, formato inválido): fica o ícone de música.
            if (load == _artworkLoad)
            {
                SetArtwork(null);
            }
        }
    }

    private void SetArtwork(ImageSource? source)
    {
        SmallArt.Source = source;
        PeekArt.Source = source;
        LargeArt.Source = source;
        ArtPlaceholder.Visibility = source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool ShowsCollapsedMedia => _showsCollapsedMedia && _media?.IsPlaying == true;

    private void UpdateCollapsedMedia()
    {
        var visible = ShowsCollapsedMedia;
        SmallArtFrame.Visibility = CollapsedControls.Visibility = Bars.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible)
        {
            _pointerOverCollapsedControls = false;
        }
    }

    private void UpdateBars(bool run)
    {
        if (run == _barsRunning)
        {
            return;
        }

        _barsRunning = run;
        if (run)
        {
            _bars.Begin();
        }
        else
        {
            _bars.Stop();
        }
    }

    private Storyboard CreateBarsAnimation()
    {
        var storyboard = new Storyboard();
        (Microsoft.UI.Xaml.Shapes.Rectangle Bar, double From, double To, int Milliseconds)[] bars =
        [
            (Bar1, 0.3, 1.0, 420),
            (Bar2, 0.5, 0.9, 560),
            (Bar3, 0.25, 0.8, 480),
        ];

        foreach (var (bar, from, to, milliseconds) in bars)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(milliseconds)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTarget(animation, bar.RenderTransform);
            Storyboard.SetTargetProperty(animation, "ScaleY");
            storyboard.Children.Add(animation);
        }

        return storyboard;
    }

    private static DoubleAnimation CreateAnimation(DependencyObject target, string property, double to, EasingFunctionBase? ease, Duration? duration = null)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = duration ?? ShapeDuration,
            EasingFunction = ease,
            EnableDependentAnimation = true, // Width/Height não animam na thread de composição.
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    private void OnIslandPointerEntered(object sender, PointerRoutedEventArgs e) => HoverChanged?.Invoke(this, true);

    private void OnIslandPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (IsPointerInside(Island, e))
        {
            return;
        }

        _pointerOverCollapsedControls = false;
        HoverChanged?.Invoke(this, false);
    }

    private static bool IsPointerInside(FrameworkElement element, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(element).Position;
        return point.X >= 0 && point.Y >= 0 && point.X < element.ActualWidth && point.Y < element.ActualHeight;
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e) => MediaCommandRequested?.Invoke(this, MediaCommand.Previous);

    private void OnPlayPauseClick(object sender, RoutedEventArgs e) => MediaCommandRequested?.Invoke(this, MediaCommand.TogglePlayPause);

    private void OnCollapsedControlsPointerEntered(object sender, PointerRoutedEventArgs e) => _pointerOverCollapsedControls = true;

    private void OnCollapsedControlsPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _pointerOverCollapsedControls = false;
        HoverChanged?.Invoke(this, true);
    }

    private void OnNextClick(object sender, RoutedEventArgs e) => MediaCommandRequested?.Invoke(this, MediaCommand.Next);

    private void ShowPage(NotchPage page)
    {
        MediaPage.Visibility = Visible(page == NotchPage.Media);
        ControlsPage.Visibility = Visible(page == NotchPage.Controls);
        DevicesPage.Visibility = Visible(page == NotchPage.Devices);
        StoragePage.Visibility = Visible(page == NotchPage.Storage);
        SystemPage.Visibility = Visible(page == NotchPage.System);
        ClipboardPage.Visibility = Visible(page == NotchPage.Clipboard);
        LinksPage.Visibility = Visible(page == NotchPage.Links);

        // O ToggleButton se inverte sozinho no clique: aqui todas as abas ficam no estado da página real.
        MediaTab.IsChecked = page == NotchPage.Media;
        ControlsTab.IsChecked = page is NotchPage.Controls or NotchPage.Devices;
        StorageTab.IsChecked = page == NotchPage.Storage;
        SystemTab.IsChecked = page == NotchPage.System;
        ClipboardTab.IsChecked = page == NotchPage.Clipboard;
        LinksTab.IsChecked = page == NotchPage.Links;

        if (page != _page)
        {
            _page = page;
            PageChanged?.Invoke(this, page);
        }

        static Visibility Visible(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMediaTabClick(object sender, RoutedEventArgs e) => ShowPage(NotchPage.Media);

    private void OnControlsTabClick(object sender, RoutedEventArgs e) => ShowPage(NotchPage.Controls);

    private void OnStorageTabClick(object sender, RoutedEventArgs e) => ShowPage(NotchPage.Storage);

    private void OnSystemTabClick(object sender, RoutedEventArgs e) => ShowPage(NotchPage.System);

    private void OnClipboardTabClick(object sender, RoutedEventArgs e) => ShowPage(NotchPage.Clipboard);

    private void OnLinksTabClick(object sender, RoutedEventArgs e) => ShowPage(NotchPage.Links);

    private void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id })
        {
            return;
        }

        if (id.Length == 0)
        {
            LinkAddRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            LinkOpenRequested?.Invoke(this, id);
        }
    }

    public void SetLinks(IReadOnlyList<QuickLink> links, Func<string, string?> iconPath)
    {
        var tiles = new List<LinkTile>(links.Count + 1);
        var alive = new HashSet<string>();
        foreach (var link in links)
        {
            var tile = LinkTile.For(link);
            tiles.Add(tile);
            if (iconPath(link.Id) is { } path)
            {
                alive.Add(path);
                _ = ShowLinkIconAsync(tile, path);
            }
        }

        foreach (var stale in _linkIcons.Keys.Where(k => !alive.Contains(k)).ToList())
        {
            _linkIcons.Remove(stale);
        }

        if (links.Count < QuickLinkList.MaxLinks)
        {
            tiles.Add(LinkTile.Add());
        }

        LinkList.ItemsSource = tiles;
        NoLinksText.Visibility = links.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task ShowLinkIconAsync(LinkTile tile, string path)
    {
        try
        {
            var stamp = File.GetLastWriteTimeUtc(path);
            if (_linkIcons.TryGetValue(path, out var cached) && cached.Stamp == stamp)
            {
                tile.ShowIcon(cached.Image);
                return;
            }

            if (await LinkIconLoader.LoadAsync(path, 64) is not { } image)
            {
                _linkIcons.Remove(path);
                return;
            }

            _linkIcons[path] = (stamp, image);
            tile.ShowIcon(image);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _linkIcons.Remove(path);
        }
    }

    private void OnClipboardItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: long id })
        {
            ClipboardCopyRequested?.Invoke(this, id);
        }
    }

    private void OnClipboardRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: long id })
        {
            ClipboardRemoveRequested?.Invoke(this, id);
        }
    }

    private void OnClipboardClearClick(object sender, RoutedEventArgs e) => ClipboardClearRequested?.Invoke(this, EventArgs.Empty);

    private void OnDriveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string root })
        {
            DriveOpenRequested?.Invoke(this, root);
        }
    }

    // O ToggleButton já se inverteu no clique: volta ao estado real; o novo chega por SetConnectivity quando mudar.
    private void OnWiFiClick(object sender, RoutedEventArgs e)
    {
        if (_link.Kind == NetworkKind.Ethernet)
        {
            WiFiButton.IsChecked = true;
            QuickActionRequested?.Invoke(this, QuickAction.OpenEthernetSettings);
            return;
        }

        WiFiButton.IsChecked = _wifi.Power == RadioPower.On;
        QuickActionRequested?.Invoke(this, QuickAction.ToggleWiFi);
    }

    private void OnBluetoothClick(object sender, RoutedEventArgs e)
    {
        BluetoothButton.IsChecked = _bluetooth.Power == RadioPower.On;
        QuickActionRequested?.Invoke(this, QuickAction.ToggleBluetooth);
    }

    private void OnWiFiRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
        QuickActionRequested?.Invoke(this, _link.Kind == NetworkKind.Ethernet ? QuickAction.OpenEthernetSettings : QuickAction.OpenWiFiList);
    }

    private void OnBluetoothRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
        QuickActionRequested?.Invoke(this, QuickAction.OpenBluetoothList);
    }

    private void OnMuteClick(object sender, RoutedEventArgs e) => QuickActionRequested?.Invoke(this, QuickAction.ToggleMute);

    private void OnVolumeSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_updatingVolume)
        {
            VolumeSliderMoved?.Invoke(this, e.NewValue);
        }
    }

    private void OnOutputClick(object sender, RoutedEventArgs e) => ShowPage(NotchPage.Devices);

    private void OnDevicesBackClick(object sender, RoutedEventArgs e) => ShowPage(NotchPage.Controls);

    private void OnDeviceClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AudioOutputDevice device)
        {
            OutputDeviceSelected?.Invoke(this, device);
        }

        ShowPage(NotchPage.Controls);
    }
}
