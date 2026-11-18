using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Gearbox.Core.Settings;
using Gearbox.Modules.Notch.Models;
using Gearbox.Modules.Notch.Services.AI;
using Gearbox.Modules.Notch.Services.Links;
using Gearbox.Modules.Notch.Services.Storage;
using Gearbox.Modules.Notch.Views.Controls;
using Gearbox.Modules.Notch.Views.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace Gearbox.Modules.Notch.ViewModels;

public sealed partial class NotchSettingsViewModel : ObservableObject
{
    private readonly ISettingsService<NotchSettings> _settings;
    private readonly ISettingsService<AiUsageSettings> _aiSettings;
    private readonly ISettingsService<ChatSettings> _chatSettings;
    private readonly OpenRouterAccount _openRouter;
    private readonly ILogger<NotchSettingsViewModel> _logger;
    private readonly LinkCatalog _links;
    private readonly LinkEditor _linkEditor;
    private readonly DispatcherQueueTimer? _saveTimer;
    private readonly bool _isLoading;
    private string _backgroundColor;
    private bool _useAccentColor;

    public NotchSettingsViewModel(
        ISettingsService<NotchSettings> settings,
        ISettingsService<AiUsageSettings> aiSettings,
        ISettingsService<ChatSettings> chatSettings,
        OpenRouterAccount openRouter,
        LinkCatalog links,
        LinkEditor linkEditor,
        ILogger<NotchSettingsViewModel> logger)
    {
        _settings = settings;
        _aiSettings = aiSettings;
        _chatSettings = chatSettings;
        _openRouter = openRouter;
        _openRouter.Changed += OnOpenRouterChanged;
        _links = links;
        _linkEditor = linkEditor;
        _logger = logger;
        _links.Changed += OnLinksChanged;
        RefreshLinks();

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
        ShowMixer = current.ShowMixer;
        ShowStorage = current.ShowStorage;
        ShowSystemInfo = current.ShowSystemInfo;
        ShowClipboard = current.ShowClipboard;
        ShowLinks = current.ShowLinks;
        ShowAiUsage = current.ShowAiUsage;
        var ai = aiSettings.Load();
        AnthropicBudget = (double)ai.AnthropicMonthlyBudget;
        OpenAiBudget = (double)ai.OpenAiMonthlyBudget;
        RefreshKeys();
        ShowChat = current.ShowChat;
        var chat = chatSettings.Load();
        KeyCreditLimit = (double)chat.KeyCreditLimit;
        AllowPaidModels = chat.AllowPaidModels;
        AllowDataCollection = chat.AllowDataCollection;
        RefreshOpenRouter();
        HideOnFullscreen = current.HideOnFullscreen;
        CompactWhenCovered = current.CompactWhenCovered;
        CollapseToHandle = current.CollapseToHandle;
        ShowMediaWhenCollapsed = current.ShowMediaWhenCollapsed;
        BackgroundStyleIndex = (int)current.BackgroundStyle;
        _backgroundColor = current.BackgroundColor;
        _useAccentColor = current.UseAccentColor;
        BuildSwatches();
        RoundnessIndex = (int)current.Roundness;
        SpringAnimation = current.SpringAnimation;
        SpringIntensityIndex = (int)current.SpringIntensity;
        GlassBorder = current.GlassBorder;
        AdaptTextContrast = current.AdaptTextContrast;
        _isLoading = false;
    }

    public ObservableCollection<ColorSwatch> Swatches { get; } = [];

    public ObservableCollection<LinkRow> LinkRows { get; } = [];

    [ObservableProperty]
    public partial bool HasNoLinks { get; set; }

    [ObservableProperty]
    public partial int BackgroundStyleIndex { get; set; }

    [ObservableProperty]
    public partial object? SelectedSwatch { get; set; }

    [ObservableProperty]
    public partial int RoundnessIndex { get; set; }

    [ObservableProperty]
    public partial bool SpringAnimation { get; set; }

    [ObservableProperty]
    public partial int SpringIntensityIndex { get; set; }

    [ObservableProperty]
    public partial bool GlassBorder { get; set; }

    [ObservableProperty]
    public partial bool AdaptTextContrast { get; set; }


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
    public partial bool ShowMixer { get; set; }

    [ObservableProperty]
    public partial bool ShowStorage { get; set; }

    [ObservableProperty]
    public partial bool ShowSystemInfo { get; set; }

    [ObservableProperty]
    public partial bool ShowClipboard { get; set; }

    [ObservableProperty]
    public partial bool ShowLinks { get; set; }

    [ObservableProperty]
    public partial bool ShowAiUsage { get; set; }

    [ObservableProperty]
    public partial double AnthropicBudget { get; set; }

    [ObservableProperty]
    public partial double OpenAiBudget { get; set; }

    [ObservableProperty]
    public partial string AnthropicKeyStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OpenAiKeyStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowChat { get; set; }

    [ObservableProperty]
    public partial double KeyCreditLimit { get; set; }

    [ObservableProperty]
    public partial bool AllowPaidModels { get; set; }

    [ObservableProperty]
    public partial bool AllowDataCollection { get; set; }

    [ObservableProperty]
    public partial string OpenRouterStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OpenRouterConnectLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsOpenRouterConnected { get; set; }

    [ObservableProperty]
    public partial bool CanEditCreditLimit { get; set; }

    [ObservableProperty]
    public partial bool HideOnFullscreen { get; set; }

    [ObservableProperty]
    public partial bool CompactWhenCovered { get; set; }

    [ObservableProperty]
    public partial bool CollapseToHandle { get; set; }

    [ObservableProperty]
    public partial bool ShowMediaWhenCollapsed { get; set; }

    partial void OnShowMediaChanged(bool value) => Save();

    partial void OnShowVolumeChanged(bool value) => Save();

    partial void OnShowBatteryChanged(bool value) => Save();

    partial void OnShowConnectivityChanged(bool value) => Save();

    partial void OnShowAudioControlsChanged(bool value) => Save();

    partial void OnShowMixerChanged(bool value) => Save();

    partial void OnShowStorageChanged(bool value) => Save();

    partial void OnShowSystemInfoChanged(bool value) => Save();

    partial void OnShowClipboardChanged(bool value) => Save();

    partial void OnShowLinksChanged(bool value) => Save();

    partial void OnShowAiUsageChanged(bool value) => Save();

    partial void OnShowChatChanged(bool value) => Save();

    partial void OnKeyCreditLimitChanged(double value) =>
        SaveChat(settings => settings.KeyCreditLimit = double.IsFinite(value) && value > 0 ? Math.Round((decimal)value, 2) : 0m);

    partial void OnAllowPaidModelsChanged(bool value) => SaveChat(settings => settings.AllowPaidModels = value);

    partial void OnAllowDataCollectionChanged(bool value) => SaveChat(settings => settings.AllowDataCollection = value);

    private void SaveChat(Action<ChatSettings> change)
    {
        if (_isLoading)
        {
            return;
        }

        try
        {
            var settings = _chatSettings.Load();
            change(settings);
            _chatSettings.Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Não foi possível salvar as configurações do chat");
        }
    }

    partial void OnAnthropicBudgetChanged(double value) => SaveAiUsage();

    partial void OnOpenAiBudgetChanged(double value) => SaveAiUsage();

    partial void OnHideOnFullscreenChanged(bool value) => Save();

    partial void OnCompactWhenCoveredChanged(bool value) => Save();

    partial void OnCollapseToHandleChanged(bool value) => Save();

    partial void OnShowMediaWhenCollapsedChanged(bool value) => Save();

    partial void OnBackgroundStyleIndexChanged(int oldValue, int newValue) => SaveIndex(oldValue, newValue, value => BackgroundStyleIndex = value);

    partial void OnSelectedSwatchChanged(object? oldValue, object? newValue)
    {
        if (newValue is not ColorSwatch swatch)
        {
            if (oldValue is not null && !_isLoading)
            {
                SelectedSwatch = oldValue;
            }

            return;
        }

        _useAccentColor = swatch.Kind == ColorSwatchKind.Accent;
        if (!_useAccentColor)
        {
            _backgroundColor = swatch.Color.ToHex();
        }

        Save();
    }


    partial void OnRoundnessIndexChanged(int oldValue, int newValue) => SaveIndex(oldValue, newValue, value => RoundnessIndex = value);

    partial void OnSpringAnimationChanged(bool value) => Save();

    partial void OnSpringIntensityIndexChanged(int oldValue, int newValue) => SaveIndex(oldValue, newValue, value => SpringIntensityIndex = value);

    partial void OnGlassBorderChanged(bool value) => Save();

    partial void OnAdaptTextContrastChanged(bool value) => Save();


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

    private static T ToEnum<T>(int index, T fallback)
        where T : struct, Enum
    {
        var value = (T)Enum.ToObject(typeof(T), index);
        return Enum.IsDefined(value) ? value : fallback;
    }

    public void SaveKey(AiProvider provider, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        try
        {
            AiKeyVault.Set(provider, key);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Não foi possível guardar a chave de {Provider}", provider);
        }

        RefreshKeys();
        SaveAiUsage();
    }

    public void RemoveKey(AiProvider provider)
    {
        AiKeyVault.Remove(provider);
        RefreshKeys();
        SaveAiUsage();
    }

    private void RefreshKeys()
    {
        AnthropicKeyStatus = KeyStatus(AiProvider.Anthropic);
        OpenAiKeyStatus = KeyStatus(AiProvider.OpenAi);

        static string KeyStatus(AiProvider provider) =>
            ModuleStrings.Get(AiKeyVault.Has(provider) ? "AiKeySaved" : "AiKeyMissing");
    }

    private void SaveAiUsage()
    {
        if (_isLoading)
        {
            return;
        }

        try
        {
            _aiSettings.Save(new AiUsageSettings
            {
                AnthropicMonthlyBudget = ToBudget(AnthropicBudget),
                OpenAiMonthlyBudget = ToBudget(OpenAiBudget),
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Não foi possível salvar o orçamento de IA");
        }

        static decimal ToBudget(double value) => double.IsFinite(value) && value > 0 ? Math.Round((decimal)value, 2) : 0m;
    }

    public void ToggleOpenRouterConnection()
    {
        switch (_openRouter.State)
        {
            case OpenRouterConnection.Connecting:
                _openRouter.CancelConnect();
                break;
            case OpenRouterConnection.Connected:
                _openRouter.Disconnect();
                break;
            default:
                _ = _openRouter.ConnectAsync(_chatSettings.Load().KeyCreditLimit);
                break;
        }
    }

    private void OnOpenRouterChanged(object? sender, EventArgs e) => RefreshOpenRouter();

    private void RefreshOpenRouter()
    {
        var state = _openRouter.State;
        IsOpenRouterConnected = state == OpenRouterConnection.Connected;
        CanEditCreditLimit = state == OpenRouterConnection.Disconnected;
        OpenRouterStatus = _openRouter.LastErrorKey is { } error
            ? ModuleStrings.Get(error)
            : ModuleStrings.Get(state switch
            {
                OpenRouterConnection.Connected => "OpenRouterStatusConnected",
                OpenRouterConnection.Connecting => "OpenRouterStatusConnecting",
                _ => "OpenRouterStatusDisconnected",
            });
        OpenRouterConnectLabel = ModuleStrings.Get(state switch
        {
            OpenRouterConnection.Connected => "OpenRouterDisconnectButton",
            OpenRouterConnection.Connecting => "ChatConnectCancel",
            _ => "ChatConnectButton",
        });
    }

    public void AddLink() => _linkEditor.Open();

    public void RemoveLink(string id) => _links.Remove(id);

    public void MoveLink(string id, int offset) => _links.Move(id, offset);

    public void Detach()
    {
        _links.Changed -= OnLinksChanged;
        _openRouter.Changed -= OnOpenRouterChanged;
    }

    private void OnLinksChanged(object? sender, EventArgs e) => RefreshLinks();

    private void RefreshLinks()
    {
        LinkRows.Clear();
        foreach (var link in _links.Links)
        {
            var row = new LinkRow(link);
            LinkRows.Add(row);
            if (_links.IconPath(link.Id) is { } path)
            {
                _ = LoadIconAsync(row, path);
            }
        }

        HasNoLinks = LinkRows.Count == 0;
    }

    private static async Task LoadIconAsync(LinkRow row, string path) => row.Icon = await LinkIconLoader.LoadAsync(path, 32);

    public void UseCustomColor(TintColor color)
    {
        var hex = color.ToHex();
        var preset = Swatches.FirstOrDefault(s => s.Kind == ColorSwatchKind.Preset && s.Color.ToHex() == hex);
        if (preset is not null)
        {
            SelectedSwatch = preset;
            return;
        }

        var custom = new ColorSwatch(ColorSwatchKind.Custom, color, ModuleStrings.Get("ColorCustom"));
        var index = Swatches.ToList().FindIndex(s => s.Kind == ColorSwatchKind.Custom);
        if (index >= 0)
        {
            Swatches[index] = custom;
        }
        else
        {
            Swatches.Add(custom);
        }

        SelectedSwatch = custom;
    }

    private void BuildSwatches()
    {
        foreach (var preset in NotchAppearance.Palette)
        {
            Swatches.Add(new ColorSwatch(ColorSwatchKind.Preset, preset.Color, ModuleStrings.Get("Color" + preset.Key)));
        }

        var accent = new UISettings().GetColorValue(UIColorType.AccentDark2);
        Swatches.Add(new ColorSwatch(ColorSwatchKind.Accent, new TintColor(accent.R, accent.G, accent.B), ModuleStrings.Get("ColorAccent")));

        var current = NotchAppearance.ParseOrDefault(_backgroundColor);
        var match = _useAccentColor
            ? Swatches.First(s => s.Kind == ColorSwatchKind.Accent)
            : Swatches.FirstOrDefault(s => s.Kind == ColorSwatchKind.Preset && s.Color == current);
        if (match is null)
        {
            match = new ColorSwatch(ColorSwatchKind.Custom, current, ModuleStrings.Get("ColorCustom"));
            Swatches.Add(match);
        }

        SelectedSwatch = match;
    }

    private void SaveIndex(int oldValue, int newValue, Action<int> restore)
    {
        if (newValue < 0)
        {
            if (oldValue >= 0)
            {
                restore(oldValue);
            }

            return;
        }

        Save();
    }

    private void Save()
    {
        if (_isLoading)
        {
            return;
        }

        _saveTimer?.Stop();

        try
        {
            _settings.Save(Snapshot());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Não foi possível salvar as configurações do Notch");
        }
    }

    private NotchSettings Snapshot() =>
        new()
        {
            ShowMedia = ShowMedia,
            ShowVolume = ShowVolume,
            ShowBattery = ShowBattery,
            ShowConnectivity = ShowConnectivity,
            ShowAudioControls = ShowAudioControls,
            ShowMixer = ShowMixer,
            ShowStorage = ShowStorage,
            ShowSystemInfo = ShowSystemInfo,
            ShowClipboard = ShowClipboard,
            ShowLinks = ShowLinks,
            ShowAiUsage = ShowAiUsage,
            ShowChat = ShowChat,
            HideOnFullscreen = HideOnFullscreen,
            CompactWhenCovered = CompactWhenCovered,
            CollapseToHandle = CollapseToHandle,
            ShowMediaWhenCollapsed = ShowMediaWhenCollapsed,
            BackgroundStyle = ToEnum(BackgroundStyleIndex, BackgroundStyle.Glass),
            BackgroundColor = _backgroundColor,
            UseAccentColor = _useAccentColor,
            Roundness = ToEnum(RoundnessIndex, NotchRoundness.Default),
            SpringAnimation = SpringAnimation,
            SpringIntensity = ToEnum(SpringIntensityIndex, SpringIntensity.Normal),
            GlassBorder = GlassBorder,
            AdaptTextContrast = AdaptTextContrast,
        };
}
