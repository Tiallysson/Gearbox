using Gearbox.Modules.Notch.Models;

namespace Gearbox.Modules.Notch.Tests.Models;

public sealed class BoltSchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0.0, 8.0)]
    [InlineData(0.5, 11.5)]
    [InlineData(1.0, 15.0)]
    [InlineData(-3.0, 8.0)]
    [InlineData(7.0, 15.0)]
    public void Intervalo_EntreOitoEQuinzeMinutos(double random, double minutes)
    {
        var scheduler = new BoltScheduler(() => random);

        Assert.Equal(TimeSpan.FromMinutes(minutes), scheduler.NextDelay());
    }

    [Fact]
    public void SemInteracaoHaQuinzeMinutos_Dorme()
    {
        var scheduler = new BoltScheduler(() => 0.0);

        var appearance = scheduler.Choose(Now, Now - TimeSpan.FromMinutes(15));

        Assert.Equal(BoltMood.Sleeping, appearance.Mood);
        Assert.StartsWith("BoltPhraseSleeping", appearance.PhraseKey, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.0, BoltMood.Happy)]
    [InlineData(0.5, BoltMood.EarTwitch)]
    [InlineData(0.99, BoltMood.Yawn)]
    [InlineData(1.0, BoltMood.Yawn)]
    public void Acordado_EscolheUmHumorEUmaFraseDele(double random, BoltMood expected)
    {
        var scheduler = new BoltScheduler(() => random);

        var appearance = scheduler.Choose(Now, Now - TimeSpan.FromMinutes(2));

        Assert.Equal(expected, appearance.Mood);
        Assert.Matches(@"^BoltPhrase" + expected + @"\d$", appearance.PhraseKey);
    }

    [Theory]
    [InlineData(0.0, "BoltPhraseHappy1")]
    [InlineData(0.999, "BoltPhraseHappy4")]
    [InlineData(1.0, "BoltPhraseHappy4")]
    public void Frase_FicaDentroDaQuantidadeDoHumor(double random, string expected)
    {
        var values = new Queue<double>([0.0, random]);
        var scheduler = new BoltScheduler(() => values.Dequeue());

        Assert.Equal(expected, scheduler.Choose(Now, Now).PhraseKey);
    }

    [Fact]
    public void Aparece_SoComAIlhaRecolhidaLivreENormal()
    {
        Assert.True(BoltScheduler.CanAppear(NotchMode.Collapsed, PeekKind.None, NotchVisibility.Normal, false, false));
        Assert.False(BoltScheduler.CanAppear(NotchMode.Expanded, PeekKind.None, NotchVisibility.Normal, false, false));
        Assert.False(BoltScheduler.CanAppear(NotchMode.Peek, PeekKind.Volume, NotchVisibility.Normal, false, false));
        Assert.False(BoltScheduler.CanAppear(NotchMode.Collapsed, PeekKind.None, NotchVisibility.Compact, false, false));
        Assert.False(BoltScheduler.CanAppear(NotchMode.Collapsed, PeekKind.None, NotchVisibility.Hidden, false, false));
        Assert.False(BoltScheduler.CanAppear(NotchMode.Collapsed, PeekKind.None, NotchVisibility.Normal, sessionLocked: true, false));
        Assert.False(BoltScheduler.CanAppear(NotchMode.Collapsed, PeekKind.None, NotchVisibility.Normal, false, displayOff: true));
    }

    [Theory]
    [InlineData("Claro! Aqui vai a explicação.", "Claro! Aqui vai a explicação.")]
    [InlineData("\n\n## **Resumo** rápido\nresto", "Resumo rápido")]
    [InlineData("- primeiro item\n- segundo", "primeiro item")]
    [InlineData("1. passo `um`", "passo um")]
    [InlineData("```csharp\nvar x = 1;\n```", "var x = 1;")]
    [InlineData("> citação importante", "citação importante")]
    [InlineData("", "")]
    public void Previa_PrimeiraLinhaSemMarkdown(string reply, string expected)
    {
        Assert.Equal(expected, BoltScheduler.Preview(reply));
    }

    [Fact]
    public void Previa_LongaECortada()
    {
        var preview = BoltScheduler.Preview(new string('a', 200));

        Assert.Equal(BoltScheduler.PreviewLength, preview.Length);
        Assert.EndsWith("…", preview, StringComparison.Ordinal);
    }
}
