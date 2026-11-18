using System.Text;
using System.Text.Json.Serialization;

namespace Gearbox.Modules.Notch.Logic;

public enum ChatRole
{
    User,
    Assistant,
    System,
}

public sealed record ChatMessage(
    ChatRole Role,
    string Content,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int RedactedSecrets = 0);

public sealed class ChatConversation
{
    public const string DefaultModel = ChatGuardrails.FreeRouter;

    private const int TitleLength = 40;

    public string Id { get; set; } = NewId();

    public string Title { get; set; } = string.Empty;

    public string Model { get; set; } = DefaultModel;

    public DateTimeOffset UpdatedAt { get; set; }

    public List<ChatMessage> Messages { get; set; } = [];

    public static string NewId() => Guid.NewGuid().ToString("N");

    public static bool IsValidId(string? id) =>
        id is { Length: 32 } && id.All(char.IsAsciiHexDigitLower);

    public static string TitleFrom(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var part in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(part);
        }

        var flat = builder.ToString();
        return flat.Length <= TitleLength ? flat : flat[..(TitleLength - 1)].TrimEnd() + "…";
    }
}

public static class ChatContext
{
    public const int DefaultCharacterBudget = 24_000;

    public static List<ChatMessage> Window(IReadOnlyList<ChatMessage> messages, int characterBudget = DefaultCharacterBudget)
    {
        var picked = new List<ChatMessage>();
        var used = 0;
        for (var index = messages.Count - 1; index >= 0; index--)
        {
            var message = messages[index];
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            if (picked.Count > 0 && used + message.Content.Length > characterBudget)
            {
                break;
            }

            used += message.Content.Length;
            picked.Add(message);
        }

        picked.Reverse();
        while (picked.Count > 1 && picked[0].Role == ChatRole.Assistant)
        {
            picked.RemoveAt(0);
        }

        return picked;
    }
}
