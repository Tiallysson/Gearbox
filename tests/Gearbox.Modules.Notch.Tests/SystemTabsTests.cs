using System.Globalization;
using Gearbox.Interop;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests;

public sealed class SystemTabsTests
{
    private static readonly CultureInfo s_ptBr = CultureInfo.GetCultureInfo("pt-BR");

    [Theory]
    [InlineData(false, false, 6u, NetworkKind.None)]
    [InlineData(true, true, 71u, NetworkKind.WiFi)]
    [InlineData(true, false, 6u, NetworkKind.Ethernet)]
    [InlineData(true, false, 243u, NetworkKind.Other)]
    public void Classify_PeloPerfilEPeloAdaptador(bool hasProfile, bool isWlan, uint iana, NetworkKind expected)
    {
        Assert.Equal(expected, NetworkLinks.Classify(hasProfile, isWlan, iana));
    }

    [Theory]
    [InlineData(0ul, "")]
    [InlineData(100_000_000ul, "100 Mbps")]
    [InlineData(1_000_000_000ul, "1 Gbps")]
    [InlineData(2_500_000_000ul, "2,5 Gbps")]
    [InlineData(56_000ul, "56 Kbps")]
    public void FormatSpeed_ComoOWindows(ulong bps, string expected)
    {
        Assert.Equal(expected, NetworkLinks.FormatSpeed(bps, s_ptBr));
    }

    [Theory]
    [InlineData(0d, "0 B")]
    [InlineData(512d, "512 B")]
    [InlineData(1536d, "1,5 KB")]
    [InlineData(500d * 1024 * 1024 * 1024, "500 GB")]
    [InlineData(1.8 * 1024 * 1024 * 1024 * 1024, "1,8 TB")]
    [InlineData(-5d, "0 B")]
    public void ByteFormat_Base1024(double bytes, string expected)
    {
        Assert.Equal(expected, ByteFormat.Format(bytes, s_ptBr));
    }

    [Fact]
    public void DriveUsage_PorcentagemELetra()
    {
        var drive = new DriveUsage(@"C:\", "Sistema", 1000, 250, IsRemovable: false);

        Assert.Equal(75, drive.UsedPercent, 3);
        Assert.Equal("C:", drive.Letter);
    }

    [Fact]
    public void DriveUsage_TamanhoZero_NaoDivide()
    {
        Assert.Equal(0, new DriveUsage(@"E:\", "", 0, 0, IsRemovable: true).UsedPercent);
    }

    [Fact]
    public void RingGeometry_ComecaNoTopo()
    {
        var arc = RingGeometry.For(25, size: 52, thickness: 4);

        Assert.Equal(26, arc.StartX, 6);
        Assert.Equal(2, arc.StartY, 6); // Metade da espessura abaixo da borda.
        Assert.Equal(24, arc.Radius, 6);
    }

    [Fact]
    public void RingGeometry_QuartoDeVolta_TerminaNaDireita()
    {
        var arc = RingGeometry.For(25, size: 52, thickness: 4);

        Assert.Equal(50, arc.EndX, 6);
        Assert.Equal(26, arc.EndY, 6);
        Assert.False(arc.IsLargeArc);
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(75, true)]
    public void RingGeometry_ArcoGrandeSoAcimaDaMetade(double percent, bool large)
    {
        Assert.Equal(large, RingGeometry.For(percent, 52, 4).IsLargeArc);
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(-10, true, false)]
    [InlineData(100, false, true)]
    [InlineData(150, false, true)]
    [InlineData(double.NaN, true, false)]
    public void RingGeometry_Extremos(double percent, bool empty, bool full)
    {
        var arc = RingGeometry.For(percent, 52, 4);

        Assert.Equal(empty, arc.IsEmpty);
        Assert.Equal(full, arc.IsFull);
    }

    [Fact]
    public void TryParseEngine_SeparaPlacaEMotor()
    {
        Assert.True(GpuCounters.TryParseEngine("pid_1234_luid_0x00000000_0x0000D1E5_phys_0_eng_3_engtype_3D", out var luid, out var engine));

        Assert.Equal("luid_0x00000000_0x0000D1E5", luid);
        Assert.Equal("phys_0_eng_3_engtype_3D", engine);
    }

    [Theory]
    [InlineData("")]
    [InlineData("_Total")]
    [InlineData("pid_1_phys_0_luid_0x0_0x1")]
    public void TryParseEngine_ForaDoFormato(string instance)
    {
        Assert.False(GpuCounters.TryParseEngine(instance, out _, out _));
    }

    private const string Gpu = "luid_0x00000000_0x0000D1E5";
    private const string OtherGpu = "luid_0x00000000_0x0000AAAA";

    [Fact]
    public void UsageByAdapter_ProcessosNoMesmoMotor_Somam()
    {
        // Chrome (30%) e um jogo (50%) no mesmo motor 3D: a segunda amostra repete a chave (o Add quebraria aqui).
        var usage = GpuCounters.UsageByAdapter(
        [
            new CounterSample($"pid_100_{Gpu}_phys_0_eng_0_engtype_3D", 30),
            new CounterSample($"pid_200_{Gpu}_phys_0_eng_0_engtype_3D", 50),
        ]);

        Assert.Equal(80, usage[Gpu], 6);
    }

    [Fact]
    public void UsageByAdapter_MotoresDiferentes_ValeOMaisOcupado()
    {
        var usage = GpuCounters.UsageByAdapter(
        [
            new CounterSample($"pid_100_{Gpu}_phys_0_eng_0_engtype_3D", 80),
            new CounterSample($"pid_100_{Gpu}_phys_0_eng_1_engtype_Copy", 5),
            new CounterSample($"pid_300_{Gpu}_phys_0_eng_5_engtype_VideoEncode", 40),
        ]);

        Assert.Equal(80, usage[Gpu], 6); // Não 125: motores trabalham em paralelo.
    }

    [Fact]
    public void UsageByAdapter_PlacasSeparadas()
    {
        var usage = GpuCounters.UsageByAdapter(
        [
            new CounterSample($"pid_100_{Gpu}_phys_0_eng_0_engtype_3D", 70),
            new CounterSample($"pid_100_{OtherGpu}_phys_0_eng_0_engtype_3D", 10),
        ]);

        Assert.Equal(70, usage[Gpu], 6);
        Assert.Equal(10, usage[OtherGpu], 6);
    }

    [Fact]
    public void UsageByAdapter_IgnoraInstanciasForaDoFormato()
    {
        var usage = GpuCounters.UsageByAdapter([new CounterSample("_Total", 99)]);

        Assert.Empty(usage);
    }

    [Fact]
    public void UsageByAdapter_LuidSemDiferenciarMaiusculas()
    {
        var usage = GpuCounters.UsageByAdapter([new CounterSample($"pid_1_{Gpu}_phys_0_eng_0_engtype_3D", 12)]);

        Assert.Equal(12, usage[Gpu.ToLowerInvariant()], 6); // O DXGI e o PDH podem escrever o hexadecimal diferente.
    }

    [Fact]
    public void MemoryByAdapter_SomaAsPartesFisicas()
    {
        var memory = GpuCounters.MemoryByAdapter(
        [
            new CounterSample("luid_0x00000000_0x0000D1E5_phys_0", 100),
            new CounterSample("luid_0x00000000_0x0000D1E5_phys_1", 50),
            new CounterSample("luid_0x00000000_0x0000AAAA_phys_0", 7),
        ]);

        Assert.Equal(150, memory["luid_0x00000000_0x0000D1E5"]);
        Assert.Equal(7, memory["luid_0x00000000_0x0000AAAA"]);
    }

    [Fact]
    public void HottestZone_ConverteKelvinEIgnoraLeiturasInvalidas()
    {
        var hottest = Temperatures.HottestZoneCelsius(
        [
            new CounterSample(@"\_TZ.TZ00", 318.15), // 45 °C
            new CounterSample(@"\_TZ.TZ01", 333.15), // 60 °C
            new CounterSample(@"\_TZ.TZ02", 0), // Zona desligada.
            new CounterSample(@"\_TZ.TZ03", 500), // Defeito (~227 °C).
        ]);

        Assert.Equal(60, hottest!.Value, 6);
    }

    [Fact]
    public void HottestZone_SemZonas_Nulo()
    {
        Assert.Null(Temperatures.HottestZoneCelsius([]));
    }
}
