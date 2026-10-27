using CommunityToolkit.Mvvm.ComponentModel;
using Gearbox.Core.Settings;
using Gearbox.Modules.Notch.Logic;
using Microsoft.UI.Dispatching;

namespace Gearbox.Modules.Notch.UI;

public sealed partial class NotchSettingsViewModel : ObservableObject
{
    private readonly ISettingsService<NotchSettings> _settings;
    private readonly DispatcherQueueTimer? _saveTimer;
    private readonly bool _isLoading;

    public NotchSettingsViewModel(ISettingsService<NotchSettings> settings)
    {
        _settings = settings;

        // O slider dispara dezenas de mudanças por segundo: salva (e aplica) com um pequeno atraso.
        _saveTimer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();
        if (_saveTimer is not null)
        {
            _saveTimer.Interval = TimeSpan.FromMilliseconds(150);
            _saveTimer.IsRepeating = false;
            _saveTimer.Tick += (_, _) => Save();
        }

        _isLoading = true;
        var current = settings.Load();
        ShowMedia = current.ShowMedia;
        ShowVolume = current.ShowVolume;
        ShowBattery = current.ShowBattery;
        ShowConnectivity = current.ShowConnectivity;
        ShowAudioControls = current.ShowAudioControls;
        ShowStorage = current.ShowStorage;
        ShowSystemInfo = current.ShowSystemInfo;
        HideOnFullscreen = current.HideOnFullscreen;
        CompactWhenCovered = current.CompactWhenCovered;
        AcrylicBackground = current.AcrylicBackground;
        GlassBorder = current.GlassBorder;
        BackgroundOpacity = NotchAppearance.ClampOpacity(current.BackgroundOpacity);
        _isLoading = false;
    }

    public double MinOpacity => NotchAppearance.MinOpacity;

    public double MaxOpacity => NotchAppearance.MaxOpacity;

    [ObservableProperty]
    public partial bool AcrylicBackground { get; set; }

    [ObservableProperty]
    public partial bool GlassBorder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundOpacityText))]
    public partial double BackgroundOpacity { get; set; }

    public string BackgroundOpacityText => ModuleStrings.Format("PercentFormat", (int)Math.Round(BackgroundOpacity));

    [ObservableProperty]
    public partial bool ShowMedia { get; set; }

    [ObservableProperty]
    public partial bool ShowVolume { get; set; }

    [ObservableProperty]
    public partial bool ShowBattery { get; set; }

    [ObservableProperty]
    public partial bool ShowConnectivity { get; set; }

    [ObservableProperty]
    public partial bool ShowAudioControls { get; set; }

    [ObservableProperty]
    public partial bool ShowStorage { get; set; }

    [ObservableProperty]
    public partial bool ShowSystemInfo { get; set; }

    [ObservableProperty]
    public partial bool HideOnFullscreen { get; set; }

    [ObservableProperty]
    public partial bool CompactWhenCovered { get; set; }

    partial void OnShowMediaChanged(bool value) => Save();

    partial void OnShowVolumeChanged(bool value) => Save();

    partial void OnShowBatteryChanged(bool value) => Save();

    partial void OnShowConnectivityChanged(bool value) => Save();

    partial void OnShowAudioControlsChanged(bool value) => Save();

    partial void OnShowStorageChanged(bool value) => Save();

    partial void OnShowSystemInfoChanged(bool value) => Save();

    partial void OnHideOnFullscreenChanged(bool value) => Save();

    partial void OnCompactWhenCoveredChanged(bool value) => Save();

    partial void OnAcrylicBackgroundChanged(bool value) => Save();

    partial void OnGlassBorderChanged(bool value) => Save();

    partial void OnBackgroundOpacityChanged(double value) => ScheduleSave();

    /// <summary>Grava imediatamente o que estiver pendente (ex.: ao sair da página).</summary>
    public void Flush()
    {
        if (_saveTimer?.IsRunning == true)
        {
            _saveTimer.Stop();
            Save();
        }
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
        if (_isLoading)
        {
            return;
        }

        _saveTimer?.Stop();

        _settings.Save(new NotchSettings
        {
            ShowMedia = ShowMedia,
            ShowVolume = ShowVolume,
            ShowBattery = ShowBattery,
            ShowConnectivity = ShowConnectivity,
            ShowAudioControls = ShowAudioControls,
            ShowStorage = ShowStorage,
            ShowSystemInfo = ShowSystemInfo,
            HideOnFullscreen = HideOnFullscreen,
            CompactWhenCovered = CompactWhenCovered,
            AcrylicBackground = AcrylicBackground,
            GlassBorder = GlassBorder,
            BackgroundOpacity = NotchAppearance.ClampOpacity((int)Math.Round(BackgroundOpacity)),
        });
    }
}
