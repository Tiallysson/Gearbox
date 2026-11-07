using System.Runtime.InteropServices;
using Gearbox.Core.Updates;

namespace Gearbox.Core.Tests.Updates;

public sealed class UpdateSelectorTests
{
    private const string Hash = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";

    private static readonly AppVersion s_current = new(0, 3, 0, "");

    [Fact]
    public void Select_EscolheAMaiorVersaoNova()
    {
        ReleaseInfo[] releases = [Release("0.3.1"), Release("0.5.0"), Release("0.4.0")];

        var candidate = UpdateSelector.Select(s_current, releases, Architecture.X64, includePrereleases: false);

        Assert.Equal(new AppVersion(0, 5, 0, ""), candidate?.Release.Version);
        Assert.Equal("Gearbox-0.5.0-x64-setup.exe", candidate?.Installer.Name);
    }

    [Fact]
    public void Select_IgnoraVersoesIguaisOuMenores()
    {
        ReleaseInfo[] releases = [Release("0.3.0"), Release("0.2.0")];

        Assert.Null(UpdateSelector.Select(s_current, releases, Architecture.X64, includePrereleases: false));
    }

    [Fact]
    public void Select_IgnoraDrafts()
    {
        ReleaseInfo[] releases = [Release("0.4.0") with { IsDraft = true }];

        Assert.Null(UpdateSelector.Select(s_current, releases, Architecture.X64, includePrereleases: true));
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "0.4.0-alpha")]
    public void Select_PrereleaseSoComOptIn(bool includePrereleases, string? expected)
    {
        ReleaseInfo[] releases = [Release("0.4.0-alpha") with { IsPrerelease = true }];

        var candidate = UpdateSelector.Select(s_current, releases, Architecture.X64, includePrereleases);

        Assert.Equal(expected, candidate?.Release.Version.ToString());
    }

    [Fact]
    public void Select_TagComSufixoContaComoPrereleaseMesmoSemAFlag()
    {
        ReleaseInfo[] releases = [Release("0.4.0-beta")];

        Assert.Null(UpdateSelector.Select(s_current, releases, Architecture.X64, includePrereleases: false));
    }

    [Fact]
    public void Select_UsaOInstaladorDaArquitetura()
    {
        ReleaseInfo[] releases = [Release("0.4.0")];

        var candidate = UpdateSelector.Select(s_current, releases, Architecture.Arm64, includePrereleases: false);

        Assert.Equal("Gearbox-0.4.0-arm64-setup.exe", candidate?.Installer.Name);
    }

    [Fact]
    public void Select_ArquiteturaSemInstalador_NaoOfereceNada()
    {
        ReleaseInfo[] releases = [Release("0.4.0")];

        Assert.Null(UpdateSelector.Select(s_current, releases, Architecture.X86, includePrereleases: false));
    }

    [Fact]
    public void Select_IgnoraInstaladorSemHashOuSemHttps()
    {
        var release = Release("0.4.0");
        ReleaseInfo[] releases =
        [
            release with { Assets = [release.Assets[0] with { Sha256 = null }] },
            Release("0.5.0") with { Assets = [new ReleaseAsset("Gearbox-0.5.0-x64-setup.exe", new Uri("http://x/a.exe"), 1, Hash)] },
        ];

        Assert.Null(UpdateSelector.Select(s_current, releases, Architecture.X64, includePrereleases: false));
    }

    [Theory]
    [InlineData(false, "", false)]
    [InlineData(true, "", true)]
    [InlineData(false, "alpha", true)]
    public void IncludesPrereleases_VersaoAtualDeTesteForcaInclusao(bool optedIn, string currentPrerelease, bool expected)
    {
        Assert.Equal(expected, UpdateSelector.IncludesPrereleases(optedIn, new AppVersion(0, 4, 0, currentPrerelease)));
    }

    private static ReleaseInfo Release(string version)
    {
        Assert.True(AppVersion.TryParse(version, out var parsed));
        return new ReleaseInfo(
            $"v{version}",
            parsed,
            IsPrerelease: false,
            IsDraft: false,
            new Uri($"https://github.com/Tiallysson/Gearbox/releases/tag/v{version}"),
            [
                Asset($"Gearbox-{version}-x64-setup.exe"),
                Asset($"Gearbox-{version}-arm64-setup.exe"),
            ]);
    }

    private static ReleaseAsset Asset(string name) =>
        new(name, new Uri($"https://github.com/Tiallysson/Gearbox/releases/download/{name}"), 1, Hash);
}
