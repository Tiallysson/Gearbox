using System.Text.Json;

namespace Gearbox.Modules.Notch.Models;

public readonly record struct ChatStreamEvent(string? Delta, string? Error, bool IsDone);

public static class ChatStream
{
    private const string DataPrefix = "data:";
    private const string DoneMarker = "[DONE]";

    public static ChatStreamEvent? ParseLine(string? line)
    {
        if (line is null || !line.StartsWith(DataPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var data = line[DataPrefix.Length..].Trim();
        if (data == DoneMarker)
        {
            return new ChatStreamEvent(null, null, IsDone: true);
        }

        try
        {
            using var document = JsonDocument.Parse(data);
            return FromPayload(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? ErrorMessage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ErrorOf(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ChatStreamEvent? FromPayload(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (ErrorOf(root) is { } error)
        {
            return new ChatStreamEvent(null, error, IsDone: true);
        }

        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var choice = choices[0];
        string? delta = null;
        if (choice.TryGetProperty("delta", out var deltaElement) &&
            deltaElement.ValueKind == JsonValueKind.Object &&
            deltaElement.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.String)
        {
            delta = content.GetString();
        }

        if (choice.TryGetProperty("finish_reason", out var finish) && finish.ValueKind == JsonValueKind.String &&
            finish.GetString() == "error")
        {
            return new ChatStreamEvent(delta, string.Empty, IsDone: true);
        }

        return string.IsNullOrEmpty(delta) ? null : new ChatStreamEvent(delta, null, IsDone: false);
    }

    private static string? ErrorOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var error))
        {
            return null;
        }

        return error.ValueKind switch
        {
            JsonValueKind.Object when error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String =>
                message.GetString() ?? string.Empty,
            JsonValueKind.String => error.GetString() ?? string.Empty,
            _ => string.Empty,
        };
    }
}
