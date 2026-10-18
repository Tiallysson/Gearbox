namespace Gearbox.Core;

/// <summary>
/// Pastas de dados do Gearbox. O app é unpackaged, então nada de <c>ApplicationData.Current</c>:
/// tudo fica em <c>%LocalAppData%\Gearbox</c>.
/// </summary>
public sealed class GearboxPaths(string rootDirectory)
{
    public static GearboxPaths Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gearbox"));

    public string RootDirectory { get; } = rootDirectory;

    public string SettingsDirectory => Path.Combine(RootDirectory, "settings");

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public string GetSettingsFile(string id) => Path.Combine(SettingsDirectory, $"{id}.json");
}
