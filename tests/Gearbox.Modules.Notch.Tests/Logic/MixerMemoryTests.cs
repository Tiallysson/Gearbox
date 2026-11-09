using Gearbox.Modules.Notch.Logic;
using static Gearbox.Modules.Notch.Tests.Logic.MixerAppsTests;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class MixerMemoryTests
{
    private const string Spotify = @"C:\Spotify\Spotify.exe";

    [Fact]
    public void With_NaoAlteraOOriginal()
    {
        var original = new Dictionary<string, MixerAppPreference> { ["a.exe"] = new(Volume: 0.5f) };

        var next = MixerMemory.With(original, "a.exe", preference => preference with { Muted = true });

        Assert.Equal(new MixerAppPreference(0.5f, true), next["a.exe"]);
        Assert.Equal(new MixerAppPreference(0.5f), original["a.exe"]);
    }

    [Fact]
    public void With_AppNovo_ComecaVazio()
    {
        var next = MixerMemory.With(new Dictionary<string, MixerAppPreference>(), "a.exe", preference => preference with { Volume = 0.3f });

        Assert.Equal(new MixerAppPreference(0.3f), next["a.exe"]);
    }

    [Fact]
    public void Valid_DescartaEntradasNulasEChavesDeProcesso()
    {
        var stored = new Dictionary<string, MixerAppPreference?>
        {
            ["spotify.exe"] = new(0.5f),
            ["discord.exe"] = null,
            ["pid:7"] = new(0.2f),
            [""] = new(0.1f),
        };

        var valid = MixerMemory.Valid(stored);

        Assert.Equal(["spotify.exe"], valid.Keys);
    }

    [Fact]
    public void Valid_ArquivoSemApps_Vazio()
    {
        Assert.Empty(MixerMemory.Valid(null));
    }

    [Fact]
    public void Fresh_SoSessoesAindaNaoVistas()
    {
        var fresh = MixerMemory.Fresh([Session("a", Spotify), Session("b", Spotify)], new Dictionary<string, string> { ["a"] = "fone" });

        Assert.Equal(["b"], fresh.Select(session => session.InstanceId));
    }

    [Fact]
    public void Known_EsqueceSessoesQueSumiramDeSaidasLidas()
    {
        var known = MixerMemory.Known(
            new Dictionary<string, string> { ["velha"] = "fone" },
            [Session("nova", Spotify, device: "fone")],
            new HashSet<string>());

        Assert.Equal(["nova"], known.Keys);
    }

    [Fact]
    public void Known_FalhaAoLerUmaSaida_LembraAsSessoesDela()
    {
        var known = MixerMemory.Known(
            new Dictionary<string, string> { ["a"] = "caixa", ["b"] = "fone" },
            [Session("c", Spotify, device: "fone")],
            new HashSet<string> { "caixa" });

        Assert.Equal(["a", "c"], known.Keys.Order());
        Assert.Empty(MixerMemory.Fresh([Session("a", Spotify, device: "caixa")], known));
    }

    [Fact]
    public void Planos_IgnoramAppsIdentificadosSoPeloProcesso()
    {
        var preferences = new Dictionary<string, MixerAppPreference> { ["pid:10"] = new(0.2f, true, "caixa") };
        var session = Session("a", null, processId: 10);

        Assert.Empty(MixerMemory.VolumePlan([session], preferences));
        Assert.Empty(MixerMemory.RoutePlan([session], preferences, new HashSet<string> { "caixa" }));
    }

    [Fact]
    public void VolumePlan_ReaplicaVolumeEMudoSalvos()
    {
        var preferences = new Dictionary<string, MixerAppPreference> { ["spotify.exe"] = new(0.4f, true) };

        var plan = MixerMemory.VolumePlan([Session("a", Spotify, volume: 1f, muted: false)], preferences);

        Assert.Equal([new SessionRestore("a", 0.4f, true)], plan);
    }

    [Fact]
    public void VolumePlan_JaNoValorSalvo_NaoFazNada()
    {
        var preferences = new Dictionary<string, MixerAppPreference> { ["spotify.exe"] = new(0.4f, false) };

        Assert.Empty(MixerMemory.VolumePlan([Session("a", Spotify, volume: 0.401f)], preferences));
    }

    [Fact]
    public void VolumePlan_AppSemPreferencia_NaoFazNada()
    {
        Assert.Empty(MixerMemory.VolumePlan([Session("a", Spotify, volume: 0.2f)], new Dictionary<string, MixerAppPreference>()));
    }

    [Fact]
    public void RoutePlan_LevaOAppParaASaidaSalvaUmaVezPorProcesso()
    {
        var preferences = new Dictionary<string, MixerAppPreference> { ["spotify.exe"] = new(DeviceId: "caixa") };

        var plan = MixerMemory.RoutePlan(
            [Session("a", Spotify, processId: 7), Session("b", Spotify, processId: 7)],
            preferences,
            new HashSet<string> { "caixa", "fone" });

        Assert.Equal([new AppRoute(7, "caixa")], plan);
    }

    [Fact]
    public void RoutePlan_SaidaDesconectada_NaoTroca()
    {
        var preferences = new Dictionary<string, MixerAppPreference> { ["spotify.exe"] = new(DeviceId: "caixa") };

        Assert.Empty(MixerMemory.RoutePlan([Session("a", Spotify)], preferences, new HashSet<string> { "fone" }));
    }

    [Fact]
    public void RoutePlan_JaNaSaidaSalva_NaoTroca()
    {
        var preferences = new Dictionary<string, MixerAppPreference> { ["spotify.exe"] = new(DeviceId: "fone") };

        Assert.Empty(MixerMemory.RoutePlan([Session("a", Spotify, device: "fone")], preferences, new HashSet<string> { "fone" }));
    }

    [Fact]
    public void RouteApp_TodosOsProcessosDoApp()
    {
        var routes = MixerMemory.RouteApp(
            [Session("a", Spotify, processId: 1), Session("b", Spotify, processId: 2), Session("c", @"C:\x\outro.exe", processId: 3)],
            "spotify.exe",
            null);

        Assert.Equal([new AppRoute(1, null), new AppRoute(2, null)], routes);
    }

    [Fact]
    public void Reset_SoDesfazOQueOGearboxMudou()
    {
        var plan = MixerMemory.Reset(
            [
                Session("a", Spotify, processId: 1),
                Session("b", Spotify, processId: 1),
                Session("c", @"C:\d\Discord.exe", processId: 2),
                Session("d", @"C:\z\Zoom.exe", processId: 3),
            ],
            new HashSet<string> { "spotify.exe" },
            new HashSet<string> { "discord.exe" });

        Assert.Equal(["a", "b"], plan.RestoreSessions);
        Assert.Equal([2u], plan.UnrouteProcesses);
    }

    [Fact]
    public void Reset_NadaMudado_PlanoVazio()
    {
        var plan = MixerMemory.Reset([Session("a", Spotify)], new HashSet<string>(), new HashSet<string>());

        Assert.Empty(plan.RestoreSessions);
        Assert.Empty(plan.UnrouteProcesses);
    }
}
