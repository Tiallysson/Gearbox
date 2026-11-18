using System.Globalization;
using Gearbox.Modules.Notch.Models;

namespace Gearbox.Modules.Notch.ViewModels;

public sealed class AiUsageItem(AiUsage usage, DateTime updatedAt)
{
    private static readonly NumberFormatInfo s_money = UsDollarsFormat();

    public double Percent => usage.Status == AiUsageStatus.Ok ? usage.Percent : 0;

    public string Name => usage.Provider switch
    {
        AiProvider.Anthropic => ModuleStrings.Get("AiProviderAnthropic"),
        AiProvider.OpenAi => ModuleStrings.Get("AiProviderOpenAi"),
        AiProvider.OpenRouter => ModuleStrings.Get("AiProviderOpenRouter"),
        _ => ModuleStrings.Get("AiProviderTotal"),
    };

    public string Caption => usage.Status switch
    {
        AiUsageStatus.InvalidKey => ModuleStrings.Get("AiInvalidKey"),
        AiUsageStatus.Unavailable => ModuleStrings.Get("AiUnavailable"),
        _ when !usage.HasBudget => ModuleStrings.Format("AiNoBudgetFormat", Money(usage.Spent)),
        _ when usage.IsOverBudget => ModuleStrings.Format("AiOverBudgetFormat", Money(usage.Spent - usage.Budget)),
        _ => ModuleStrings.Format("AiRemainingFormat", Money(usage.Remaining)),
    };

    public string ToolTip => usage.Status switch
    {
        AiUsageStatus.InvalidKey => ModuleStrings.Format("AiInvalidKeyToolTipFormat", Name),
        AiUsageStatus.Unavailable => ModuleStrings.Format("AiUnavailableToolTipFormat", Name),
        _ when !usage.HasBudget => ModuleStrings.Format(
            "AiNoBudgetToolTipFormat",
            Name,
            Money(usage.Spent),
            updatedAt.ToString("t", CultureInfo.CurrentCulture)),
        _ => ModuleStrings.Format(
            "AiToolTipFormat",
            Name,
            Money(usage.Spent),
            Money(usage.Budget),
            Money(usage.Remaining),
            updatedAt.ToString("t", CultureInfo.CurrentCulture)),
    };

    private static string Money(decimal value) => value.ToString("C2", s_money);

    private static NumberFormatInfo UsDollarsFormat()
    {
        var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        format.CurrencySymbol = "US$";
        return format;
    }
}
