using System.Text.Json;

namespace Gearbox.Modules.Notch.Models;

public sealed record ChatModel(string Id, string Name);

public static class ChatModels
{
    public static List<ChatModel> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var models = new List<ChatModel>();
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return models;
        }

        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idElement) || idElement.GetString() is not { Length: > 0 } id ||
                !ProducesText(item))
            {
                continue;
            }

            var name = item.TryGetProperty("name", out var nameElement) && nameElement.GetString() is { Length: > 0 } named
                ? named
                : id;
            models.Add(new ChatModel(id, name));
        }

        return models
            .DistinctBy(m => m.Id, StringComparer.Ordinal)
            .OrderBy(m => m.Id == ChatConversation.DefaultModel ? 0 : 1)
            .ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static List<ChatModel> WithSelected(List<ChatModel> models, string selectedId)
    {
        if (models.Exists(m => m.Id == selectedId))
        {
            return models;
        }

        return [new ChatModel(selectedId, selectedId), .. models];
    }

    private static bool ProducesText(JsonElement item)
    {
        if (!item.TryGetProperty("architecture", out var architecture) ||
            !architecture.TryGetProperty("output_modalities", out var outputs) ||
            outputs.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        return outputs.EnumerateArray().Any(o => o.ValueKind == JsonValueKind.String && o.GetString() == "text");
    }
}
