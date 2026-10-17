using Microsoft.Windows.ApplicationModel.Resources;

namespace Darwin.Modules.RoundedMaximize;

/// <summary>Textos do módulo (<c>Strings\pt-BR\Resources.resw</c> deste projeto).</summary>
internal static class ModuleStrings
{
    // Em bibliotecas, os .resw ficam num mapa próprio dentro do .pri do app: "{Assembly}/Resources".
    private static readonly Lazy<ResourceLoader?> s_loader = new(() =>
    {
        try
        {
            return new ResourceLoader(ResourceLoader.GetDefaultResourceFilePath(), "Darwin.Modules.RoundedMaximize/Resources");
        }
        catch (Exception)
        {
            return null;
        }
    });

    public static string Get(string key)
    {
        try
        {
            var value = s_loader.Value?.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch (Exception)
        {
            return key;
        }
    }

    public static string Format(string key, params object[] args) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, Get(key), args);
}
