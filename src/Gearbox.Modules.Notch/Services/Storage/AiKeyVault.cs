using System.Runtime.InteropServices;
using Gearbox.Modules.Notch.Models;
using Windows.Security.Credentials;

namespace Gearbox.Modules.Notch.Services.Storage;

internal static class AiKeyVault
{
    private const string Resource = "Gearbox.AiUsage";

    public static string? Get(AiProvider provider)
    {
        try
        {
            var credential = new PasswordVault().Retrieve(Resource, UserName(provider));
            credential.RetrievePassword();
            return string.IsNullOrWhiteSpace(credential.Password) ? null : credential.Password;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static bool Has(AiProvider provider) => Get(provider) is not null;

    public static void Set(AiProvider provider, string key)
    {
        Remove(provider);
        new PasswordVault().Add(new PasswordCredential(Resource, UserName(provider), key.Trim()));
    }

    public static void Remove(AiProvider provider)
    {
        try
        {
            var vault = new PasswordVault();
            vault.Remove(vault.Retrieve(Resource, UserName(provider)));
        }
        catch (COMException)
        {
        }
    }

    private static string UserName(AiProvider provider) => provider.ToString();
}
