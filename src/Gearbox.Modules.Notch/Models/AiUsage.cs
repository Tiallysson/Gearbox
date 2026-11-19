namespace Gearbox.Modules.Notch.Models;

public sealed record AiUsage(AiProvider? Provider, AiUsageStatus Status, decimal Spent, decimal Budget)
{
    public double Percent => Budget <= 0 ? 0 : (double)Math.Min(100m, Math.Max(0m, Spent) / Budget * 100m);

    public decimal Remaining => Math.Max(0m, Budget - Math.Max(0m, Spent));

    public bool IsOverBudget => Budget > 0 && Spent > Budget;

    public bool HasBudget => Budget > 0;

    public static AiUsage? Total(IEnumerable<AiUsage> usages)
    {
        var known = usages.Where(u => u.Provider is not null && u.Status == AiUsageStatus.Ok && u.HasBudget).ToList();
        return known.Count < 2
            ? null
            : new AiUsage(null, AiUsageStatus.Ok, known.Sum(u => u.Spent), known.Sum(u => u.Budget));
    }
}
