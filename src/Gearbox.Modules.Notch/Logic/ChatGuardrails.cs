using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gearbox.Modules.Notch.Logic;

public enum ChatViolation
{
    None,
    Busy,
    EmptyInput,
    InputTooLong,
    PaidModelBlocked,
    UsageLimitReached,
}

public sealed record KeyUsage(decimal Usage, decimal? Limit, decimal? LimitRemaining)
{
    public double? Percent
    {
        get
        {
            if (Limit is not > 0m)
            {
                return null;
            }

            var used = LimitRemaining is { } remaining ? Limit.Value - remaining : Usage;
            return (double)(Math.Max(0m, used) / Limit.Value * 100m);
        }
    }
}

public sealed record SecretRedaction(string Text, int Count);

public static partial class ChatGuardrails
{
    public const int MaxTokens = 1500;
    public const int MaxInputLength = 12_000;
    public const double UsageBlockPercent = 98;
    public const string FreeRouter = "openrouter/free";
    public const string ContextTag = "contexto_usuario";
    public const string RedactedSecret = "[REDACTED_SECRET]";
    public const int DefaultHistoryMessages = 6;
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);

    private const string FreeSuffix = ":free";

    public static bool IsFree(string? modelId) =>
        modelId is not null &&
        (string.Equals(modelId, FreeRouter, StringComparison.Ordinal) || modelId.EndsWith(FreeSuffix, StringComparison.Ordinal));

    public static ChatViolation CheckRequest(string modelId, bool allowPaidModels, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ChatViolation.EmptyInput;
        }

        if (input.Length > MaxInputLength)
        {
            return ChatViolation.InputTooLong;
        }

        return allowPaidModels || IsFree(modelId) ? ChatViolation.None : ChatViolation.PaidModelBlocked;
    }

    public static ChatViolation CheckUsage(double? usagePercent) =>
        usagePercent >= UsageBlockPercent ? ChatViolation.UsageLimitReached : ChatViolation.None;

    public static string DefaultModel(string? preferred, bool allowPaidModels) =>
        string.IsNullOrWhiteSpace(preferred) || (!allowPaidModels && !IsFree(preferred)) ? FreeRouter : preferred;

    public static List<ChatModel> Allowed(List<ChatModel> models, bool allowPaidModels)
    {
        var allowed = allowPaidModels ? models : models.FindAll(m => IsFree(m.Id));
        var router = allowed.Find(m => m.Id == FreeRouter) ?? new ChatModel(FreeRouter, FreeRouter);
        return [router, .. allowed.Where(m => m.Id != FreeRouter)];
    }

    public static string SanitizeUserPrompt(string content) =>
        $"<{ContextTag}>\n{TagPattern().Replace(RedactSecrets(content).Text, "&lt;$1")}\n</{ContextTag}>";

    public static SecretRedaction RedactSecrets(string text)
    {
        var count = 0;
        var redacted = SecretPattern().Replace(text, _ =>
        {
            count++;
            return RedactedSecret;
        });
        return new SecretRedaction(redacted, count);
    }

    public static List<ChatMessage> TrimConversationHistory(List<ChatMessage> messages, int maxMessages = DefaultHistoryMessages)
    {
        var system = messages.Count > 0 && messages[0].Role == ChatRole.System ? messages[0] : null;
        var conversation = messages.Where(m => m.Role != ChatRole.System).ToList();
        var recent = conversation.Skip(Math.Max(0, conversation.Count - Math.Max(1, maxMessages))).ToList();
        while (recent.Count > 1 && recent[0].Role == ChatRole.Assistant)
        {
            recent.RemoveAt(0);
        }

        return system is null ? recent : [system, .. recent];
    }

    public static KeyUsage? ParseKeyUsage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new KeyUsage(Number(data, "usage") ?? 0m, Number(data, "limit"), Number(data, "limit_remaining"));
        }
        catch (JsonException)
        {
            return null;
        }

        static decimal? Number(JsonElement data, string name) =>
            data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)
                ? number
                : null;
    }

    public static bool IsDataPolicyError(string? message) =>
        message is not null && message.Contains("data policy", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"<(\s*/?\s*contexto_usuario)", RegexOptions.IgnoreCase)]
    private static partial Regex TagPattern();

    [GeneratedRegex(
        @"\bsk-[A-Za-z0-9_-]{16,}" +
        @"|\bgh[pousr]_[A-Za-z0-9]{20,}" +
        @"|\bgithub_pat_[A-Za-z0-9_]{20,}" +
        @"|\bAIza[0-9A-Za-z_-]{35}" +
        @"|(?<=\b(?i:bearer)\s+)(?=[A-Za-z._~+/-]*\d)[A-Za-z0-9._~+/-]{8,}=*")]
    private static partial Regex SecretPattern();
}
