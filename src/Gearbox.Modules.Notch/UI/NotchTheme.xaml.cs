using Microsoft.UI.Xaml;

namespace Gearbox.Modules.Notch.UI;

/// <summary>
/// Visual da ilha (paleta e estilos no estilo shadcn/ui). Classe em vez de <c>Source</c>: o caminho <c>ms-appx</c> do
/// XAML de uma biblioteca muda entre build e publish; o <c>InitializeComponent</c> gerado sabe onde ele está.
/// </summary>
public sealed partial class NotchTheme : ResourceDictionary
{
    public NotchTheme()
    {
        InitializeComponent();
    }
}
