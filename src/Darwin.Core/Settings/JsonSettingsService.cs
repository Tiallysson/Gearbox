using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Darwin.Core.Settings;

public sealed class JsonSettingsService<T>(string id, DarwinPaths paths, ILogger<JsonSettingsService<T>> logger)
    : ISettingsService<T>
    where T : class, new()
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly Lock _gate = new();

    public event EventHandler<T>? Changed;

    public string FilePath { get; } = paths.GetSettingsFile(id);

    public T Load()
    {
        lock (_gate)
        {
            if (!File.Exists(FilePath))
            {
                return new T();
            }

            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<T>(json, s_options) ?? new T();
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Configurações '{Id}' ilegíveis em {Path}; usando os valores padrão", id, FilePath);
                TryBackupCorruptFile();
                return new T();
            }
        }
    }

    public void Save(T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string json;
        lock (_gate)
        {
            json = JsonSerializer.Serialize(settings, s_options);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            // Grava num arquivo temporário e troca: um crash no meio não deixa JSON pela metade.
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, FilePath, overwrite: true);
        }

        logger.LogDebug("Configurações '{Id}' salvas", id);
        Changed?.Invoke(this, JsonSerializer.Deserialize<T>(json, s_options)!);
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            File.Copy(FilePath, FilePath + ".corrupt", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Não foi possível copiar o arquivo corrompido");
        }
    }
}
