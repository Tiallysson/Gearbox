using Gearbox.Modules.Notch.Models;

namespace Gearbox.Modules.Notch.Tests.Models;

public sealed class OpenRouterUsageTests
{
    [Fact]
    public void ChaveComLimite_UsaOLimiteEORestante()
    {
        var usage = OpenRouterUsage.From(new KeyUsage(Usage: 40m, Limit: 10m, LimitRemaining: 2.5m, UsageMonthly: 99m), monthlyBudget: 50m);

        Assert.Equal(AiProvider.OpenRouter, usage.Provider);
        Assert.Equal(7.5m, usage.Spent);
        Assert.Equal(10m, usage.Budget);
        Assert.Equal(75.0, usage.Percent, 6);
    }

    [Fact]
    public void ChaveComLimiteSemRestante_UsaOUsoTotal()
    {
        var usage = OpenRouterUsage.From(new KeyUsage(Usage: 4m, Limit: 5m, LimitRemaining: null), monthlyBudget: 0m);

        Assert.Equal(4m, usage.Spent);
        Assert.Equal(80.0, usage.Percent, 6);
    }

    [Fact]
    public void ChaveSemLimite_UsaOGastoDoMesEOOrcamento()
    {
        var usage = OpenRouterUsage.From(new KeyUsage(Usage: 40m, Limit: null, LimitRemaining: null, UsageMonthly: 3m), monthlyBudget: 12m);

        Assert.Equal(3m, usage.Spent);
        Assert.Equal(12m, usage.Budget);
        Assert.Equal(25.0, usage.Percent, 6);
    }

    [Fact]
    public void SemLimiteNemOrcamento_MostraSoOGasto()
    {
        var usage = OpenRouterUsage.From(new KeyUsage(Usage: 40m, Limit: 0m, LimitRemaining: null, UsageMonthly: 3m), monthlyBudget: -5m);

        Assert.False(usage.HasBudget);
        Assert.Equal(3m, usage.Spent);
        Assert.Equal(0.0, usage.Percent);
    }

    [Fact]
    public void RestanteMaiorQueOLimite_NaoFicaNegativo()
    {
        var usage = OpenRouterUsage.From(new KeyUsage(Usage: 0m, Limit: 5m, LimitRemaining: 6m), monthlyBudget: 0m);

        Assert.Equal(0m, usage.Spent);
    }

    [Fact]
    public void UsoMensal_LidoDaRespostaDaChave()
    {
        var key = ChatGuardrails.ParseKeyUsage("""{"data":{"usage":12.5,"limit":null,"limit_remaining":null,"usage_monthly":3.25}}""");

        Assert.NotNull(key);
        Assert.Equal(3.25m, key.UsageMonthly);
    }

    [Fact]
    public void Total_IgnoraProvedorSemOrcamento()
    {
        var usages = new[]
        {
            new AiUsage(AiProvider.Anthropic, AiUsageStatus.Ok, 10m, 100m),
            new AiUsage(AiProvider.OpenAi, AiUsageStatus.Ok, 20m, 100m),
            new AiUsage(AiProvider.OpenRouter, AiUsageStatus.Ok, 999m, 0m),
        };

        var total = AiUsage.Total(usages);

        Assert.NotNull(total);
        Assert.Equal(30m, total.Spent);
        Assert.Equal(200m, total.Budget);
    }
}
