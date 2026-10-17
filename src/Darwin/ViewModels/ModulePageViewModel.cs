using CommunityToolkit.Mvvm.ComponentModel;
using Darwin.Core.Modules;

namespace Darwin.ViewModels;

/// <summary>Cabeçalho comum das páginas de módulo: ícone, nome, descrição, liga/desliga e falha.</summary>
public sealed partial class ModulePageViewModel : ObservableObject, IDisposable
{
    private readonly IDarwinModule _module;
    private readonly ModuleManager _manager;
    private bool _syncing;

    public ModulePageViewModel(IDarwinModule module, ModuleManager manager)
    {
        _module = module;
        _manager = manager;
        _manager.ModuleStateChanged += OnModuleStateChanged;
        Sync();
    }

    public string Title => _module.DisplayName;

    public string Description => _module.Description;

    public string Glyph => _module.IconGlyph;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    [ObservableProperty]
    public partial bool HasFault { get; set; }

    [ObservableProperty]
    public partial string FaultMessage { get; set; } = string.Empty;

    public void Dispose() => _manager.ModuleStateChanged -= OnModuleStateChanged;

    partial void OnIsEnabledChanged(bool value)
    {
        if (_syncing)
        {
            return;
        }

        _manager.SetEnabled(_module, value);
        Sync();
    }

    private void OnModuleStateChanged(object? sender, ModuleStateChangedEventArgs e)
    {
        if (ReferenceEquals(e.Module, _module))
        {
            Sync();
        }
    }

    private void Sync()
    {
        _syncing = true;
        try
        {
            IsEnabled = _module.IsEnabled;
            var fault = _manager.GetLastFault(_module);
            HasFault = fault is not null && !_module.IsEnabled;
            FaultMessage = fault?.Message ?? string.Empty;
        }
        finally
        {
            _syncing = false;
        }
    }
}
