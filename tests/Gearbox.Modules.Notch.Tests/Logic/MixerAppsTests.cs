using Gearbox.Interop.Audio;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class MixerAppsTests
{
    private static readonly Dictionary<string, MixerAppPreference> NoPreferences = [];

    internal static AudioSessionInfo Session(
        string id,
        string? path,
        uint processId = 10,
        bool active = true,
        bool expired = false,
        float volume = 1f,
        bool muted = false,
        bool system = false,
        string device = "fone",
        string? displayName = null) =>
        new(id, processId, path, displayName, system, active, expired, volume, muted, device);

    [Fact]
    public void KeyFor_UsaONomeDoExecutavelEmMinusculas()
    {
        Assert.Equal("spotify.exe", MixerApps.KeyFor(Session("a", @"C:\Apps\Spotify\Spotify.exe")));
    }

    [Fact]
    public void KeyFor_SonsDoSistema_TemChaveFixa()
    {
        Assert.Equal(MixerApps.SystemSoundsKey, MixerApps.KeyFor(Session("a", null, processId: 0, system: true)));
    }

    [Fact]
    public void KeyFor_SemCaminho_UsaOProcesso()
    {
        Assert.Equal("pid:42", MixerApps.KeyFor(Session("a", null, processId: 42)));
    }

    [Theory]
    [InlineData("spotify.exe", true)]
    [InlineData(MixerApps.SystemSoundsKey, true)]
    [InlineData("pid:42", false)]
    public void IsPersistable_SoChavesEstaveis(string key, bool expected)
    {
        Assert.Equal(expected, MixerApps.IsPersistable(key));
    }

    [Fact]
    public void Open_MostraSessoesAbertasMesmoSemTocar_ComoOMixerDoWindows()
    {
        var apps = MixerApps.Open(
            [
                Session("a", @"C:\a\Spotify.exe"),
                Session("b", @"C:\b\Discord.exe", active: false),
                Session("c", @"C:\c\Zoom.exe", active: false, expired: true),
            ],
            MixerApps.FallbackName,
            NoPreferences);

        Assert.Equal(["discord.exe", "spotify.exe"], apps.Select(app => app.Key));
    }

    [Fact]
    public void Open_PrefereOVolumeDaSessaoTocando()
    {
        var apps = MixerApps.Open(
            [
                Session("a", @"C:\x\app.exe", processId: 1, active: false, volume: 0.2f),
                Session("b", @"C:\x\app.exe", processId: 2, active: true, volume: 0.7f),
            ],
            MixerApps.FallbackName,
            NoPreferences);

        Assert.Equal(0.7f, Assert.Single(apps).Volume);
    }

    [Fact]
    public void Open_AgrupaProcessosDoMesmoExecutavel()
    {
        var apps = MixerApps.Open(
            [
                Session("a", @"C:\Chrome\chrome.exe", processId: 1, muted: true),
                Session("b", @"C:\Chrome\chrome.exe", processId: 2, muted: false),
            ],
            MixerApps.FallbackName,
            NoPreferences);

        var app = Assert.Single(apps);
        Assert.False(app.IsMuted);
    }

    [Fact]
    public void Open_TodasNoMudo_AppNoMudo()
    {
        var apps = MixerApps.Open(
            [Session("a", @"C:\x\app.exe", processId: 1, muted: true), Session("b", @"C:\x\app.exe", processId: 2, muted: true)],
            MixerApps.FallbackName,
            NoPreferences);

        Assert.True(Assert.Single(apps).IsMuted);
    }

    [Fact]
    public void Open_SonsDoSistemaPrimeiroDepoisPorNome()
    {
        var apps = MixerApps.Open(
            [
                Session("s", null, processId: 0, system: true),
                Session("z", @"C:\z\Zoom.exe"),
                Session("a", @"C:\a\Audacity.exe"),
            ],
            session => session.IsSystemSounds ? "Sons do sistema" : MixerApps.FallbackName(session),
            NoPreferences);

        Assert.Equal([MixerApps.SystemSoundsKey, "audacity.exe", "zoom.exe"], apps.Select(app => app.Key));
    }

    [Fact]
    public void Open_TrazASaidaEscolhida()
    {
        var preferences = new Dictionary<string, MixerAppPreference> { ["spotify.exe"] = new(DeviceId: "caixa") };

        var apps = MixerApps.Open([Session("a", @"C:\a\Spotify.exe")], MixerApps.FallbackName, preferences);

        Assert.Equal("caixa", Assert.Single(apps).OutputDeviceId);
    }

    [Theory]
    [InlineData("Meu App", "Meu App")]
    [InlineData("@%SystemRoot%\\System32\\AudioSrv.Dll,-202", "Spotify")]
    [InlineData(null, "Spotify")]
    public void FallbackName_IgnoraNomesDeRecurso(string? displayName, string expected)
    {
        Assert.Equal(expected, MixerApps.FallbackName(Session("a", @"C:\a\Spotify.exe", displayName: displayName)));
    }

    [Fact]
    public void Meter_SobeNaHoraECaiAosPoucos()
    {
        Assert.Equal(0.9, MixerApps.Meter(0.2, 0.9f), 3);
        Assert.Equal(0.9 - MixerApps.MeterDecayPerTick, MixerApps.Meter(0.9, 0f), 3);
        Assert.Equal(0, MixerApps.Meter(0.01, 0f));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(-1f)]
    public void Meter_ValorInvalido_ViraZero(float peak)
    {
        Assert.Equal(0, MixerApps.Meter(0, peak));
    }
}
