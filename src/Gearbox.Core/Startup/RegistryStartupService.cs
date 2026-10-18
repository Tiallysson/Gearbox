using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Gearbox.Core.Startup;

/// <summary>Acesso mínimo a uma chave do tipo Run (abstraído para os testes).</summary>
public interface IRunKeyStore
{
    string? GetValue(string name);

    void SetValue(string name, string value);

    void DeleteValue(string name);
}

/// <summary><c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.</summary>
public sealed class CurrentUserRunKeyStore : IRunKeyStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? GetValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(name) as string;
    }

    public void SetValue(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void DeleteValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

public sealed class RegistryStartupService(IRunKeyStore store, string executablePath, ILogger<RegistryStartupService> logger)
    : IStartupService
{
    public const string ValueName = "Gearbox";
    public const string StartupArgument = "--startup";

    public RegistryStartupService(IRunKeyStore store, ILogger<RegistryStartupService> logger)
        : this(store, Environment.ProcessPath ?? throw new InvalidOperationException("Caminho do executável indisponível."), logger)
    {
    }

    public string Command => $"\"{executablePath}\" {StartupArgument}";

    public bool IsEnabled => store.GetValue(ValueName) is { Length: > 0 };

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            store.SetValue(ValueName, Command);
        }
        else
        {
            store.DeleteValue(ValueName);
        }

        logger.LogInformation("Iniciar com o Windows: {Enabled}", enabled);
    }

    public void RefreshPathIfEnabled()
    {
        var current = store.GetValue(ValueName);
        if (current is { Length: > 0 } && !string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
        {
            store.SetValue(ValueName, Command);
            logger.LogInformation("Caminho de inicialização atualizado para {Command}", Command);
        }
    }
}
