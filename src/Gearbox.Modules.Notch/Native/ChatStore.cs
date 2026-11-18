using System.Text.Json;
using System.Text.Json.Serialization;
using Gearbox.Core;
using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;

namespace Gearbox.Modules.Notch.Native;

public sealed class ChatStore(GearboxPaths paths, ILogger<ChatStore> logger)
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private readonly Lock _gate = new();

    private string Directory => Path.Combine(paths.RootDirectory, "chats");

    internal List<ChatConversation> LoadAll()
    {
        var conversations = new List<ChatConversation>();
        lock (_gate)
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return conversations;
            }

            foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
            {
                if (TryLoad(file) is { } conversation)
                {
                    conversations.Add(conversation);
                }
            }
        }

        return conversations.OrderByDescending(c => c.UpdatedAt).ToList();
    }

    internal void Save(ChatConversation conversation)
    {
        if (!ChatConversation.IsValidId(conversation.Id))
        {
            throw new ArgumentException("Id de conversa inválido", nameof(conversation));
        }

        var json = JsonSerializer.Serialize(conversation, s_options);
        lock (_gate)
        {
            System.IO.Directory.CreateDirectory(Directory);
            var path = FileOf(conversation.Id);
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);
        }
    }

    internal void Delete(string id)
    {
        if (!ChatConversation.IsValidId(id))
        {
            return;
        }

        lock (_gate)
        {
            File.Delete(FileOf(id));
        }
    }

    private string FileOf(string id) => Path.Combine(Directory, id + ".json");

    private ChatConversation? TryLoad(string file)
    {
        try
        {
            var conversation = JsonSerializer.Deserialize<ChatConversation>(File.ReadAllText(file), s_options);
            if (conversation is not null &&
                ChatConversation.IsValidId(conversation.Id) &&
                string.Equals(Path.GetFileNameWithoutExtension(file), conversation.Id, StringComparison.Ordinal))
            {
                return conversation;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Conversa ilegível em {Path}", file);
        }

        TryQuarantine(file);
        return null;
    }

    private void TryQuarantine(string file)
    {
        try
        {
            File.Move(file, file + ".corrupt", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Não foi possível separar a conversa ilegível {Path}", file);
        }
    }
}
