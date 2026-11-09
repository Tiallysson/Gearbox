using System.Globalization;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class NetworkStatsTests
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [Fact]
    public void Between_ConverteBytesPorSegundoEmBits()
    {
        var sample = NetworkRates.Between(
            new NetworkCounters(1_000, 500, TimeSpan.FromSeconds(1)),
            new NetworkCounters(126_000, 13_000, TimeSpan.FromSeconds(2)),
            14);

        Assert.Equal(1_000_000, sample.DownBitsPerSecond);
        Assert.Equal(100_000, sample.UpBitsPerSecond);
        Assert.Equal(14, sample.PingMilliseconds);
    }

    [Fact]
    public void Between_ContadorZerado_NaoFicaNegativo()
    {
        var sample = NetworkRates.Between(
            new NetworkCounters(9_000, 9_000, TimeSpan.FromSeconds(1)),
            new NetworkCounters(100, 100, TimeSpan.FromSeconds(2)),
            null);

        Assert.Equal(0, sample.DownBitsPerSecond);
        Assert.Equal(0, sample.UpBitsPerSecond);
    }

    [Fact]
    public void Between_SemTempoPassado_Zero()
    {
        var at = TimeSpan.FromSeconds(3);
        var sample = NetworkRates.Between(new NetworkCounters(0, 0, at), new NetworkCounters(500, 500, at), null);

        Assert.Equal(0, sample.DownBitsPerSecond);
    }

    [Theory]
    [InlineData(0, "0 Kbps")]
    [InlineData(double.NaN, "0 Kbps")]
    [InlineData(850_000, "850 Kbps")]
    [InlineData(12_300_000, "12,3 Mbps")]
    [InlineData(1_200_000_000, "1,2 Gbps")]
    public void Format_UnidadeLegivel(double bits, string expected)
    {
        Assert.Equal(expected, NetworkRates.Format(bits, PtBr));
    }

    [Fact]
    public void History_GuardaSoOsUltimos30Segundos()
    {
        var history = new NetworkHistory();
        for (var i = 0; i < NetworkHistory.Capacity + 5; i++)
        {
            history.Add(new NetworkSample(i, 0, null));
        }

        Assert.Equal(NetworkHistory.Capacity, history.Samples.Count);
        Assert.Equal(5, history.Samples[0].DownBitsPerSecond);
        Assert.Equal(NetworkHistory.Capacity + 4, history.Latest?.DownBitsPerSecond);
    }

    [Fact]
    public void History_LatestPing_UsaOUltimoPingConhecido()
    {
        var history = new NetworkHistory();
        history.Add(new NetworkSample(0, 0, 20));
        history.Add(new NetworkSample(0, 0, null));

        Assert.Equal(20, history.LatestPing);
    }

    [Theory]
    [InlineData(0, 1_000_000)]
    [InlineData(1_300_000, 2_000_000)]
    [InlineData(4_100_000, 5_000_000)]
    [InlineData(7_000_000, 10_000_000)]
    [InlineData(10_000_000, 10_000_000)]
    [InlineData(230_000_000, 500_000_000)]
    public void Nice_ArredondaParaCimaEm1_2_5(double value, double expected)
    {
        Assert.Equal(expected, NetworkChart.Nice(value));
    }

    [Fact]
    public void Points_MaisRecenteNaDireitaEValorNoTopoDaEscala()
    {
        var points = NetworkChart.Points([0, 5, 10], scale: 10, width: 290, height: 50);

        Assert.Equal(3, points.Count);
        Assert.Equal(new ChartPoint(290, 0), points[^1]);
        Assert.Equal(new ChartPoint(280, 25), points[1]);
        Assert.Equal(new ChartPoint(270, 50), points[0]);
    }

    [Fact]
    public void Points_HistoricoCheio_OcupaALarguraToda()
    {
        var values = Enumerable.Repeat(1.0, NetworkHistory.Capacity).ToList();

        var points = NetworkChart.Points(values, scale: 1, width: 290, height: 10);

        Assert.Equal(0, points[0].X, 6);
        Assert.Equal(290, points[^1].X, 6);
    }

    [Fact]
    public void Points_AcimaDaEscala_FicaNoTopo()
    {
        Assert.Equal(0, NetworkChart.Points([50], scale: 10, width: 100, height: 40)[0].Y);
    }

    [Fact]
    public void Points_SemEspaco_Vazio()
    {
        Assert.Empty(NetworkChart.Points([1, 2], scale: 10, width: 0, height: 40));
    }

    [Theory]
    [InlineData(true, false, false, SpeedTestDecision.Cancel)]
    [InlineData(true, true, false, SpeedTestDecision.Cancel)]
    [InlineData(false, false, false, SpeedTestDecision.Start)]
    [InlineData(false, true, false, SpeedTestDecision.ConfirmMetered)]
    [InlineData(false, true, true, SpeedTestDecision.Start)]
    public void Gate_ConexaoLimitadaPedeUmSegundoClique(bool running, bool metered, bool confirmationShown, SpeedTestDecision expected)
    {
        Assert.Equal(expected, SpeedTestGate.Decide(running, metered, confirmationShown));
    }

    [Fact]
    public void BitsPerSecond_BytesEmBits()
    {
        Assert.Equal(8_000_000, SpeedMath.BitsPerSecond(2_000_000, TimeSpan.FromSeconds(2)));
        Assert.Equal(0, SpeedMath.BitsPerSecond(100, TimeSpan.Zero));
    }

    [Fact]
    public void Median_ImparEPar()
    {
        Assert.Equal(20, SpeedMath.Median([30, 10, 20]));
        Assert.Equal(15, SpeedMath.Median([10, 20]));
        Assert.Null(SpeedMath.Median([]));
    }
}
