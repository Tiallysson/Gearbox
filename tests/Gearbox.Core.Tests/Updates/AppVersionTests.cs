using Gearbox.Core.Updates;

namespace Gearbox.Core.Tests.Updates;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("0.4.0", 0, 4, 0, "")]
    [InlineData("v0.4.0-alpha", 0, 4, 0, "alpha")]
    [InlineData("1.2.3-rc.1+abc123", 1, 2, 3, "rc.1")]
    [InlineData(" 10.20.30 ", 10, 20, 30, "")]
    [InlineData("0.4.0+3f2a1b", 0, 4, 0, "")]
    public void TryParse_VersoesValidas(string text, int major, int minor, int patch, string prerelease)
    {
        Assert.True(AppVersion.TryParse(text, out var version));
        Assert.Equal(new AppVersion(major, minor, patch, prerelease), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.x")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-alpha..1")]
    [InlineData("1.2.3-alpha_1")]
    [InlineData("-1.2.3")]
    public void TryParse_VersoesInvalidas(string? text)
    {
        Assert.False(AppVersion.TryParse(text, out _));
    }

    [Fact]
    public void Ordem_SegueAPrecedenciaDoSemver()
    {
        string[] ordered =
        [
            "1.0.0-alpha",
            "1.0.0-alpha.1",
            "1.0.0-alpha.beta",
            "1.0.0-beta",
            "1.0.0-beta.2",
            "1.0.0-beta.11",
            "1.0.0-rc.1",
            "1.0.0",
            "1.0.1",
            "1.1.0",
            "2.0.0",
        ];

        for (var i = 1; i < ordered.Length; i++)
        {
            Assert.True(AppVersion.TryParse(ordered[i - 1], out var lower));
            Assert.True(AppVersion.TryParse(ordered[i], out var higher));
            Assert.True(lower < higher, $"{lower} deveria ser menor que {higher}");
            Assert.True(higher > lower, $"{higher} deveria ser maior que {lower}");
        }
    }

    [Theory]
    [InlineData("0.4.0-alpha", "0.4.0")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.99999999999")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.-1")]
    public void Comparacao_CasosDeBorda(string lowerText, string higherText)
    {
        Assert.True(AppVersion.TryParse(lowerText, out var lower));
        Assert.True(AppVersion.TryParse(higherText, out var higher));

        Assert.True(lower < higher, $"{lower} deveria ser menor que {higher}");
    }

    [Fact]
    public void Comparacao_IgnoraBuild()
    {
        Assert.True(AppVersion.TryParse("0.4.0-alpha+aaa", out var left));
        Assert.True(AppVersion.TryParse("0.4.0-alpha+bbb", out var right));

        Assert.Equal(0, left.CompareTo(right));
    }

    [Fact]
    public void IsPrerelease_SoComSufixo()
    {
        Assert.True(new AppVersion(0, 4, 0, "alpha").IsPrerelease);
        Assert.False(new AppVersion(0, 4, 0, "").IsPrerelease);
        Assert.False(default(AppVersion).IsPrerelease);
    }
}
