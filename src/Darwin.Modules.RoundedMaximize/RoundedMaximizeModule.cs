using Darwin.Core.Modules;
using Darwin.Core.Settings;
using Darwin.Interop;
using Darwin.Modules.RoundedMaximize.Logic;
using Darwin.Modules.RoundedMaximize.Native;
using Darwin.Modules.RoundedMaximize.UI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace Darwin.Modules.RoundedMaximize;

/// <summary>
/// Janelas maximizadas ganham cantos arredondados e borda colorida, com uma pequena margem. Técnica: "falso maximizar" (ver <see cref="RoundedMaximizeEngine"/>).
/// </summary>
public sealed class RoundedMaximizeModule(
    WinEventHookManager hooks,
    ISettingsService<RoundedMaximizeSettings> settings,
    IModuleFaultReporter faults,
    ILogger<RoundedMaximizeModule> logger) : IDarwinModule
{
    public const string ModuleId = "RoundedMaximize";

    private RoundedMaximizeEngine? _engine;
    private IDisposable? _subscription;
    private UISettings? _uiSettings;
    private DispatcherQueue? _dispatcher;
    private RoundedMaximizeSettings _current = new();

    public string Id => ModuleId;

    public string DisplayName => ModuleStrings.Get("ModuleDisplayName");

    public string Description => ModuleStrings.Get("ModuleDescription");

    public string IconGlyph => ""; // FullScreen

    public bool IsEnabled { get; private set; }

    public Type SettingsPageType => typeof(RoundedMaximizeSettingsPage);

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

            _engine = new RoundedMaximizeEngine(logger);
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += OnSystemColorsChanged;

            _current = settings.Load();
            ApplySettings();
            settings.Changed += OnSettingsChanged;

            _subscription = hooks.Subscribe(Id, OnWinEvent);
            IsEnabled = true;
        }
        catch
        {
            // Desfaz o que já foi feito; o casco loga e desabilita o módulo.
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

        if (_uiSettings is not null)
        {
            _uiSettings.ColorValuesChanged -= OnSystemColorsChanged;
            _uiSettings = null;
        }

        var engine = _engine;
        _engine = null;
        engine?.RestoreAll();
    }

    private void OnWinEvent(object? sender, WinEventArgs e)
    {
        // Exceções aqui são capturadas pelo WinEventHookManager e reportadas ao casco.
        _engine?.OnWinEvent(e);
    }

    private void OnSettingsChanged(object? sender, RoundedMaximizeSettings e) => Guard(() =>
    {
        _current = e;
        ApplySettings();
    });

    private void OnSystemColorsChanged(UISettings sender, object args)
    {
        // Chega numa thread qualquer; o motor só pode ser usado na thread de UI.
        _dispatcher?.TryEnqueue(() => Guard(() =>
        {
            if (_current.UseAccentColor)
            {
                ApplySettings();
            }
        }));
    }

    private void ApplySettings()
    {
        if (_engine is null)
        {
            return;
        }

        _engine.Configure(_current.Margin, ResolveBorderColor(), _current.ExcludedProcesses);
    }

    private uint ResolveBorderColor()
    {
        if (_current.UseAccentColor)
        {
            var accent = (_uiSettings ?? new UISettings()).GetColorValue(UIColorType.Accent);
            return ColorRef.FromRgb(accent.R, accent.G, accent.B);
        }

        if (ColorRef.TryParseHex(_current.BorderColor, out var r, out var g, out var b))
        {
            return ColorRef.FromRgb(r, g, b);
        }

        logger.LogWarning("Cor de borda inválida '{Color}'; usando o padrão", _current.BorderColor);
        ColorRef.TryParseHex(RoundedMaximizeSettings.DefaultBorderColor, out r, out g, out b);
        return ColorRef.FromRgb(r, g, b);
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
