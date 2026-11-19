
namespace Gearbox.Modules.Notch.Models;

public sealed class AiUsageSettings
{
    public const string SettingsId = "NotchAiUsage";

    public decimal AnthropicMonthlyBudget { get; set; }

    public decimal OpenAiMonthlyBudget { get; set; }

    public decimal OpenRouterMonthlyBudget { get; set; }

    public decimal Budget(AiProvider provider) => provider switch
    {
        AiProvider.Anthropic => AnthropicMonthlyBudget,
        AiProvider.OpenAi => OpenAiMonthlyBudget,
        AiProvider.OpenRouter => OpenRouterMonthlyBudget,
        _ => 0m,
    };
}
