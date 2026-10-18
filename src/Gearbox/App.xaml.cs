using Gearbox.Core;
using Gearbox.Core.Logging;
using Gearbox.Core.Modules;
using Gearbox.Core.Startup;
using Gearbox.Interop;
using Gearbox.Modules.RoundedMaximize;
using Gearbox.Modules.Wallpaper;
using Gearbox.Services;
using Gearbox.ViewModels;
using Gearbox.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using Serilog.Events;

namespace Gearbox;

public partial class App : Application
{
    private IHost? _host;
    private Microsoft.Extensions.Logging.ILogger? _logger;
    private DispatcherQueue? _dispatcher;
    private bool _isShuttingDown;

    public App()
    {
        InitializeComponent();

        UnhandledException += OnXamlUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("O host ainda não foi iniciado.");

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // O app vive na bandeja: fechar (esconder) a janela não pode encerrar o processo.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _host = BuildHost();
        _host.Start();

        _logger = Services.GetRequiredService<ILoggerFactory>().CreateLogger("Gearbox");
        _logger.LogInformation(
            "Gearbox {Version} iniciado (pid {Pid}){Startup}",
            typeof(App).Assembly.GetName().Version,
            Environment.ProcessId,
            Environment.GetCommandLineArgs().Contains(RegistryStartupService.StartupArgument) ? " pela inicialização do Windows" : string.Empty);

        // O hook precisa nascer na thread de UI: é o loop de mensagens dela que entrega os eventos.
        var hooks = Services.GetRequiredService<WinEventHookManager>();
        var faultHub = Services.GetRequiredService<ModuleFaultHub>();
        hooks.HandlerFaulted += (_, e) => faultHub.ReportFault(e.OwnerId, e.Exception);

        TryRefreshStartupPath();
        Services.GetRequiredService<ThemeService>().Initialize();

        var tray = Services.GetRequiredService<TrayIconService>();
        tray.ExitRequested += (_, _) => Shutdown();
        tray.Initialize();

        Services.GetRequiredService<ModuleManager>().StartEnabledModules();

        // Uma segunda instância redireciona a ativação para cá (ver Program.cs): mostra a janela.
        AppInstance.GetCurrent().Activated += OnRedirectedActivation;
    }

    /// <summary>Encerra de verdade: devolve as janelas ao normal, remove o ícone e fecha o host.</summary>
    public void Shutdown()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        _logger?.LogInformation("Encerrando o Gearbox");

        TryRun(() => Services.GetRequiredService<ModuleManager>().StopAll(), "parar os módulos");
        TryRun(() => Services.GetRequiredService<TrayIconService>().Dispose(), "remover o ícone da bandeja");
        TryRun(() => Services.GetRequiredService<MainWindowService>().CloseForExit(), "fechar a janela");
        TryRun(() => Services.GetRequiredService<WinEventHookManager>().Dispose(), "remover o hook");
        TryRun(
            () =>
            {
                _host?.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
                _host?.Dispose(); // Também descarta o Serilog (flush dos logs).
            },
            "encerrar o host");

        Exit();
    }

    private static IHost BuildHost()
    {
        var paths = GearboxPaths.Default;
#if DEBUG
        var serilog = GearboxLogging.CreateLogger(paths, LogEventLevel.Debug);
#else
        var serilog = GearboxLogging.CreateLogger(paths);
#endif

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(serilog, dispose: true);
        builder.Services.Configure<ConsoleLifetimeOptions>(o => o.SuppressStatusMessages = true);

        builder.Services.AddGearboxCore(paths);
        builder.Services.AddSingleton<WinEventHookManager>();

        // Casco
        builder.Services.AddGearboxSettings<GeneralSettings>("general");
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<MainWindowService>();
        builder.Services.AddSingleton<TrayIconService>();
        builder.Services.AddTransient<MainWindow>();
        builder.Services.AddTransient<GeneralPage>();
        builder.Services.AddTransient<GeneralViewModel>();
        builder.Services.AddTransient<AboutPage>();
        builder.Services.AddTransient<AboutViewModel>();

        // Módulos: cada um se registra com a própria extension.
        builder.Services.AddRoundedMaximizeModule();
        builder.Services.AddWallpaperModule();

        return builder.Build();
    }

    private void OnRedirectedActivation(object? sender, AppActivationArguments e)
    {
        var exitRequested = e.Kind == ExtendedActivationKind.Launch &&
            e.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch &&
            Program.HasExitArgument(launch.Arguments);

        // Chega numa thread de background.
        _dispatcher?.TryEnqueue(() =>
        {
            if (exitRequested)
            {
                _logger?.LogInformation("Outra instância pediu para encerrar ({Argument})", Program.ExitArgument);
                Shutdown();
                return;
            }

            _logger?.LogInformation("Outra instância foi aberta; mostrando a janela");
            TryRun(() => Services.GetRequiredService<MainWindowService>().Show(), "mostrar a janela");
        });
    }

    private void TryRefreshStartupPath()
    {
        try
        {
            Services.GetRequiredService<IStartupService>().RefreshPathIfEnabled();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            _logger?.LogWarning(ex, "Não foi possível conferir a inicialização com o Windows");
        }
    }

    private void TryRun(Action action, string description)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Falha ao {Description}", description);
        }
    }

    private void OnXamlUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Última linha de defesa: loga e mantém o app vivo.
        _logger?.LogError(e.Exception, "Exceção não tratada na UI");
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        _logger?.LogCritical(e.ExceptionObject as Exception, "Exceção fatal");
        if (e.IsTerminating)
        {
            _host?.Dispose();
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Exceção não observada em Task");
        e.SetObserved();
    }
}
