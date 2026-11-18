namespace Gearbox.Modules.Notch.Models;

public static class OpenRouterUsage
{
    public static AiUsage From(KeyUsage key, decimal monthlyBudget)
    {
        if (key.Limit is { } limit && limit > 0m)
        {
            var used = key.LimitRemaining is { } remaining ? limit - remaining : key.Usage;
            return new AiUsage(AiProvider.OpenRouter, AiUsageStatus.Ok, Math.Max(0m, used), limit);
        }

        return new AiUsage(AiProvider.OpenRouter, AiUsageStatus.Ok, Math.Max(0m, key.UsageMonthly), Math.Max(0m, monthlyBudget));
    }
}
