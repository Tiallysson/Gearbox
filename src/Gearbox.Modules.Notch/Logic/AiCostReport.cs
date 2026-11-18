using System.Globalization;
using System.Text.Json;

namespace Gearbox.Modules.Notch.Logic;

public sealed record AiCostPage(decimal Dollars, string? NextPage);

public static class AiCostReport
{
    private const decimal CentsPerDollar = 100m;

    public static AiCostPage Parse(AiProvider provider, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var total = 0m;
        if (root.TryGetProperty("data", out var buckets) && buckets.ValueKind == JsonValueKind.Array)
        {
            foreach (var bucket in buckets.EnumerateArray())
            {
                if (!bucket.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var result in results.EnumerateArray())
                {
                    total += Amount(provider, result);
                }
            }
        }

        var hasMore = root.TryGetProperty("has_more", out var more) && more.ValueKind == JsonValueKind.True;
        var next = hasMore && root.TryGetProperty("next_page", out var page) && page.ValueKind == JsonValueKind.String
            ? page.GetString()
            : null;
        return new AiCostPage(total, string.IsNullOrEmpty(next) ? null : next);
    }

    private static decimal Amount(AiProvider provider, JsonElement result)
    {
        if (!result.TryGetProperty("amount", out var amount))
        {
            return 0m;
        }

        return provider switch
        {
            AiProvider.Anthropic when amount.ValueKind == JsonValueKind.String &&
                decimal.TryParse(amount.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var cents) => cents / CentsPerDollar,
            AiProvider.OpenAi when amount.ValueKind == JsonValueKind.Object &&
                amount.TryGetProperty("value", out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetDecimal(out var dollars) => dollars,
            _ => 0m,
        };
    }
}
