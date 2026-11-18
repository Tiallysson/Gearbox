using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class AiUsageTests
{
    [Theory]
    [InlineData(25, 100, 25, 75)]
    [InlineData(0, 100, 0, 100)]
    [InlineData(150, 100, 100, 0)]
    [InlineData(10, 0, 0, 0)]
    [InlineData(-5, 100, 0, 100)]
    public void PorcentagemERestante(double spent, double budget, double percent, double remaining)
    {
        var usage = new AiUsage(AiProvider.Anthropic, AiUsageStatus.Ok, (decimal)spent, (decimal)budget);

        Assert.Equal(percent, usage.Percent, 6);
        Assert.Equal((decimal)remaining, usage.Remaining);
    }

    [Fact]
    public void AcimaDoOrcamento_SoComOrcamentoDefinido()
    {
        Assert.True(new AiUsage(AiProvider.OpenAi, AiUsageStatus.Ok, 101m, 100m).IsOverBudget);
        Assert.False(new AiUsage(AiProvider.OpenAi, AiUsageStatus.Ok, 100m, 100m).IsOverBudget);
        Assert.False(new AiUsage(AiProvider.OpenAi, AiUsageStatus.Ok, 5m, 0m).IsOverBudget);
    }

    [Fact]
    public void Total_SomaSoOsProvedoresLidos()
    {
        var usages = new[]
        {
            new AiUsage(AiProvider.Anthropic, AiUsageStatus.Ok, 30m, 100m),
            new AiUsage(AiProvider.OpenAi, AiUsageStatus.Ok, 20m, 50m),
        };

        var total = AiUsage.Total(usages);

        Assert.NotNull(total);
        Assert.Null(total.Provider);
        Assert.Equal(50m, total.Spent);
        Assert.Equal(150m, total.Budget);
    }

    [Fact]
    public void Total_ComMenosDeDoisProvedoresLidos_NaoAparece()
    {
        var usages = new[]
        {
            new AiUsage(AiProvider.Anthropic, AiUsageStatus.Ok, 30m, 100m),
            new AiUsage(AiProvider.OpenAi, AiUsageStatus.InvalidKey, 0m, 50m),
        };

        Assert.Null(AiUsage.Total(usages));
        Assert.Null(AiUsage.Total([]));
    }
}
