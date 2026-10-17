namespace Darwin.Core;

/// <summary>
/// Pastas de dados do Darwin. O app é unpackaged, então nada de <c>ApplicationData.Current</c>:
/// tudo fica em <c>%LocalAppData%\Darwin</c>.
/// </summary>
public sealed class DarwinPaths(string rootDirectory)
{
    public static DarwinPaths Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Darwin"));

    public string RootDirectory { get; } = rootDirectory;

    public string SettingsDirectory => Path.Combine(RootDirectory, "settings");

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public string GetSettingsFile(string id) => Path.Combine(SettingsDirectory, $"{id}.json");
}
