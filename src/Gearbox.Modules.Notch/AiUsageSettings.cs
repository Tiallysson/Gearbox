using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch;

public sealed class AiUsageSettings
{
    public const string SettingsId = "NotchAiUsage";

    public decimal AnthropicMonthlyBudget { get; set; }

    public decimal OpenAiMonthlyBudget { get; set; }

    public decimal Budget(AiProvider provider) => provider switch
    {
        AiProvider.Anthropic => AnthropicMonthlyBudget,
        AiProvider.OpenAi => OpenAiMonthlyBudget,
        _ => 0m,
    };
}
