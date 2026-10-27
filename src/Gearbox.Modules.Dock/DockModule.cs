using Gearbox.Core.Modules;
using Gearbox.Core.Settings;
using Gearbox.Modules.Dock.Native;
using Gearbox.Modules.Dock.UI;
using Microsoft.UI.Dispatching;

namespace Gearbox.Modules.Dock;

public sealed class DockModule(ISettingsService<DockSettings> settings, IModuleFaultReporter faults) : IGearboxModule
{
    public const string ModuleId = "Dock";
    private DockWindow? _window;
    private DispatcherQueueTimer? _timer;
    public string Id => ModuleId;
    public string DisplayName => ModuleStrings.Get("ModuleDisplayName");
    public string Description => ModuleStrings.Get("ModuleDescription");
    public string IconGlyph => "\uE80F";
    public bool IsEnabled { get; private set; }
    public Type SettingsPageType => typeof(DockSettingsPage);

    public void Start()
    {
        if (IsEnabled) return;
        try
        {
            var dispatcher = DispatcherQueue.GetForCurrentThread()
                ?? throw new InvalidOperationException("O módulo precisa da thread de UI.");
            _window = new DockWindow();
            _window.Apply(settings.Load());
            settings.Changed += OnSettingsChanged;
            _timer = dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += OnTick;
            IsEnabled = true;
            _timer.Start();
        }
        catch { Cleanup(); throw; }
    }

    public void Stop() { IsEnabled = false; Cleanup(); }

    private void Cleanup()
    {
        settings.Changed -= OnSettingsChanged;
        if (_timer is not null) { _timer.Stop(); _timer.Tick -= OnTick; _timer = null; }
        var window = _window;
        _window = null;
        window?.Dispose();
    }

    private void OnSettingsChanged(object? sender, DockSettings value) => Guard(() => _window?.Apply(value));
    private void OnTick(DispatcherQueueTimer sender, object args) => Guard(() => _window?.Tick());
    private void Guard(Action action)
    {
        if (!IsEnabled) return;
        try { action(); }
        catch (Exception ex) { faults.ReportFault(Id, ex); }
    }
}
