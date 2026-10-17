using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Darwin.Core.Settings;
using Darwin.Interop;
using Darwin.Modules.RoundedMaximize.Logic;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace Darwin.Modules.RoundedMaximize.UI;

public sealed partial class RoundedMaximizeSettingsViewModel : ObservableObject
{
    private const int CountdownSeconds = 3;

    private readonly ISettingsService<RoundedMaximizeSettings> _settings;
    private readonly DispatcherQueueTimer? _saveTimer;
    private readonly bool _isLoading;

    public RoundedMaximizeSettingsViewModel(ISettingsService<RoundedMaximizeSettings> settings)
    {
        _settings = settings;

        // Slider e ColorPicker disparam dezenas de mudanças por segundo: salva (e aplica) com um pequeno atraso.
        _saveTimer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();
        if (_saveTimer is not null)
        {
            _saveTimer.Interval = TimeSpan.FromMilliseconds(150);
            _saveTimer.IsRepeating = false;
            _saveTimer.Tick += (_, _) => Save();
        }

        _isLoading = true;
        var current = settings.Load();
        Margin = PseudoMaximizeLayout.ClampMargin(current.Margin);
        UseAccentColor = current.UseAccentColor;
        BorderColor = ColorRef.TryParseHex(current.BorderColor, out var r, out var g, out var b)
            ? Color.FromArgb(255, r, g, b)
            : Color.FromArgb(255, 0x00, 0x78, 0xD4);
        ExcludedProcesses = new ObservableCollection<string>(current.ExcludedProcesses);
        _isLoading = false;
    }

    public double MinMargin => PseudoMaximizeLayout.MinMargin;

    public double MaxMargin => PseudoMaximizeLayout.MaxMargin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarginText))]
    public partial double Margin { get; set; }

    public string MarginText => ModuleStrings.Format("MarginValueFormat", (int)Math.Round(Margin));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomColorEnabled))]
    public partial bool UseAccentColor { get; set; }

    public bool IsCustomColorEnabled => !UseAccentColor;

    [ObservableProperty]
    public partial Color BorderColor { get; set; }

    public ObservableCollection<string> ExcludedProcesses { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddExclusionCommand))]
    public partial string NewExclusion { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddForegroundWindowCommand))]
    [NotifyPropertyChangedFor(nameof(ForegroundButtonText))]
    public partial int CountdownRemaining { get; set; }

    public string ForegroundButtonText => CountdownRemaining > 0
        ? ModuleStrings.Format("ForegroundCountdownFormat", CountdownRemaining)
        : ModuleStrings.Get("ForegroundButtonText");

    [ObservableProperty]
    public partial bool IsStatusOpen { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity StatusSeverity { get; set; }

    /// <summary>Grava imediatamente o que estiver pendente (ex.: ao sair da página).</summary>
    public void Flush()
    {
        if (_saveTimer?.IsRunning == true)
        {
            _saveTimer.Stop();
            Save();
        }
    }

    partial void OnMarginChanged(double value) => ScheduleSave();

    partial void OnUseAccentColorChanged(bool value) => ScheduleSave();

    partial void OnBorderColorChanged(Color value) => ScheduleSave();

    private bool CanAddExclusion() => EligibilityRules.NormalizeProcessName(NewExclusion).Length > 0;

    [RelayCommand(CanExecute = nameof(CanAddExclusion))]
    private void AddExclusion()
    {
        if (TryAddExclusion(NewExclusion))
        {
            NewExclusion = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveExclusion(string? processName)
    {
        if (processName is not null && ExcludedProcesses.Remove(processName))
        {
            ScheduleSave();
        }
    }

    private bool CanAddForegroundWindow() => CountdownRemaining == 0;

    /// <summary>Conta 3 segundos para o usuário trocar de janela e adiciona o processo em foco.</summary>
    [RelayCommand(CanExecute = nameof(CanAddForegroundWindow))]
    private async Task AddForegroundWindowAsync()
    {
        IsStatusOpen = false;
        for (var i = CountdownSeconds; i > 0; i--)
        {
            CountdownRemaining = i;
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        CountdownRemaining = 0;

        var hwnd = NativeWindow.GetForegroundWindow();
        var processId = hwnd == 0 ? 0 : NativeWindow.GetProcessId(hwnd);
        if (processId == 0)
        {
            ShowStatus(ModuleStrings.Get("ForegroundNotFound"), InfoBarSeverity.Warning);
            return;
        }

        if (processId == (uint)Environment.ProcessId)
        {
            ShowStatus(ModuleStrings.Get("ForegroundIsDarwin"), InfoBarSeverity.Informational);
            return;
        }

        var name = NativeWindow.GetProcessName(processId);
        if (name is null)
        {
            ShowStatus(ModuleStrings.Get("ForegroundNotFound"), InfoBarSeverity.Warning);
            return;
        }

        if (TryAddExclusion(name))
        {
            ShowStatus(ModuleStrings.Format("ForegroundAddedFormat", name), InfoBarSeverity.Success);
        }
        else
        {
            ShowStatus(ModuleStrings.Format("ForegroundAlreadyExcludedFormat", name), InfoBarSeverity.Informational);
        }
    }

    private bool TryAddExclusion(string? processName)
    {
        var normalized = EligibilityRules.NormalizeProcessName(processName);
        if (normalized.Length == 0 ||
            ExcludedProcesses.Any(p => string.Equals(EligibilityRules.NormalizeProcessName(p), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        ExcludedProcesses.Add(normalized);
        ScheduleSave();
        return true;
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
        _settings.Save(new RoundedMaximizeSettings
        {
            Margin = PseudoMaximizeLayout.ClampMargin((int)Math.Round(Margin)),
            UseAccentColor = UseAccentColor,
            BorderColor = ColorRef.ToHex(BorderColor.R, BorderColor.G, BorderColor.B),
            ExcludedProcesses = [.. ExcludedProcesses],
        });
    }
}
