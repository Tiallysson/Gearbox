using Darwin.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Darwin.Services;

/// <summary>Cria a janela principal sob demanda (o app começa só na bandeja) e a reaproveita depois.</summary>
public sealed class MainWindowService(IServiceProvider services)
{
    private MainWindow? _window;

    public void Show()
    {
        _window ??= services.GetRequiredService<MainWindow>();
        _window.ShowAndActivate();
    }

    /// <summary>Fecha de verdade (usado só ao sair do app).</summary>
    public void CloseForExit()
    {
        if (_window is null)
        {
            return;
        }

        _window.AllowClose = true;
        _window.Close();
        _window = null;
    }
}
