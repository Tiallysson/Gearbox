using CommunityToolkit.Mvvm.Input;
using Gearbox.Core.Modules;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;

namespace Gearbox.Services;

/// <summary>
/// Ícone na bandeja. Clique esquerdo abre a janela; o menu tem "Abrir Gearbox", um toggle por módulo e "Sair".
/// </summary>
public sealed class TrayIconService(ModuleManager modules, MainWindowService mainWindow, ILogger<TrayIconService> logger) : IDisposable
{
    private readonly Dictionary<string, ToggleMenuFlyoutItem> _toggles = new(StringComparer.OrdinalIgnoreCase);
    private TaskbarIcon? _icon;
    private System.Drawing.Icon? _iconImage;

    /// <summary>O usuário escolheu "Sair" no menu.</summary>
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = AppStrings.Get("TrayOpen"), Command = new RelayCommand(ShowWindow) });

        if (modules.Modules.Count > 0)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            foreach (var module in modules.Modules)
            {
                var item = new ToggleMenuFlyoutItem
                {
                    Text = module.DisplayName,
                    IsChecked = module.IsEnabled,
                    Command = new RelayCommand(() => Toggle(module)),
                };
                _toggles[module.Id] = item;
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(new MenuFlyoutItem { Text = AppStrings.Get("TrayExit"), Command = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty)) });

        _iconImage = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "Gearbox.ico"));
        _icon = new TaskbarIcon
        {
            ToolTipText = AppStrings.Get("TrayTooltip"),
            Icon = _iconImage,
            ContextFlyout = menu,

            // Menu nativo do Win32: não precisa de janela XAML e respeita o tema do sistema.
            ContextMenuMode = ContextMenuMode.PopupMenu,
            MenuActivation = PopupActivationMode.RightClick,
            LeftClickCommand = new RelayCommand(ShowWindow),
            NoLeftClickDelay = true,
        };

        // O ícone não está numa árvore visual, então precisa ser criado explicitamente.
        // Sem "modo de eficiência": o processo trata eventos de janela em tempo real.
        _icon.ForceCreate(enablesEfficiencyMode: false);

        modules.ModuleStateChanged += OnModuleStateChanged;
        logger.LogDebug("Ícone da bandeja criado");
    }

    public void Dispose()
    {
        modules.ModuleStateChanged -= OnModuleStateChanged;
        _icon?.Dispose();
        _icon = null;
        _iconImage?.Dispose();
        _iconImage = null;
    }

    private void ShowWindow()
    {
        // Roda dentro do WndProc do ícone: uma exceção aqui não passa pelo UnhandledException do XAML e derruba o processo.
        try
        {
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao abrir a janela pela bandeja");
        }
    }

    private void Toggle(IGearboxModule module)
    {
        var enabled = modules.SetEnabled(module, !module.IsEnabled);
        if (_toggles.TryGetValue(module.Id, out var item))
        {
            item.IsChecked = enabled;
        }
    }

    private void OnModuleStateChanged(object? sender, ModuleStateChangedEventArgs e)
    {
        if (_toggles.TryGetValue(e.Module.Id, out var item))
        {
            item.IsChecked = e.IsEnabled;
        }

        if (e.Fault is not null && _icon is not null)
        {
            _icon.ShowNotification(
                AppStrings.Get("ModuleFaultTitle"),
                AppStrings.Format("ModuleFaultMessageFormat", e.Module.DisplayName),
                NotificationIcon.Warning);
        }
    }
}
