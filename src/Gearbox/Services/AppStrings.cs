using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Gearbox.Services;

/// <summary>Textos do casco (<c>Strings\pt-BR\Resources.resw</c>).</summary>
internal static class AppStrings
{
    private static readonly Lazy<ResourceLoader?> s_loader = new(() =>
    {
        try
        {
            return new ResourceLoader();
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
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
