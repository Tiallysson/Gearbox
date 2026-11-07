using Gearbox.Core.Updates;

namespace Gearbox.Core.Tests.Updates;

public sealed class GitHubReleaseParserTests
{
    private const string Hash = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";

    [Fact]
    public void Parse_LeReleasesEAssets()
    {
        var json = $$"""
            [
              {
                "tag_name": "v0.4.0-alpha",
                "html_url": "https://github.com/Tiallysson/Gearbox/releases/tag/v0.4.0-alpha",
                "prerelease": true,
                "draft": false,
                "assets": [
                  {
                    "name": "Gearbox-0.4.0-alpha-x64-setup.exe",
                    "browser_download_url": "https://github.com/Tiallysson/Gearbox/releases/download/v0.4.0-alpha/Gearbox-0.4.0-alpha-x64-setup.exe",
                    "size": 1234,
                    "digest": "sha256:{{Hash.ToUpperInvariant()}}"
                  }
                ]
              }
            ]
            """;

        var release = Assert.Single(GitHubReleaseParser.Parse(json));

        Assert.Equal("v0.4.0-alpha", release.Tag);
        Assert.Equal(new AppVersion(0, 4, 0, "alpha"), release.Version);
        Assert.True(release.IsPrerelease);
        Assert.False(release.IsDraft);
        var asset = Assert.Single(release.Assets);
        Assert.Equal("Gearbox-0.4.0-alpha-x64-setup.exe", asset.Name);
        Assert.Equal(1234, asset.Size);
        Assert.Equal(Hash, asset.Sha256);
    }

    [Fact]
    public void Parse_IgnoraTagForaDoSemver()
    {
        const string json = """
            [
              { "tag_name": "nightly", "html_url": "https://github.com/x/y/releases/tag/nightly", "assets": [] },
              { "tag_name": "v0.3.0", "html_url": "https://github.com/x/y/releases/tag/v0.3.0", "assets": [] }
            ]
            """;

        var release = Assert.Single(GitHubReleaseParser.Parse(json));

        Assert.Equal("v0.3.0", release.Tag);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("md5:abc")]
    [InlineData("sha256:123")]
    [InlineData("sha256:zzbbccddeeff00112233445566778899aabbccddeeff00112233445566778899")]
    public void Parse_DigestAusenteOuInvalido_FicaSemHash(string? digest)
    {
        var digestJson = digest is null ? "null" : $"\"{digest}\"";
        var json = $$"""
            [
              {
                "tag_name": "v0.4.0",
                "html_url": "https://github.com/x/y/releases/tag/v0.4.0",
                "assets": [ { "name": "a.exe", "browser_download_url": "https://x/a.exe", "size": 1, "digest": {{digestJson}} } ]
              }
            ]
            """;

        var asset = Assert.Single(Assert.Single(GitHubReleaseParser.Parse(json)).Assets);

        Assert.Null(asset.Sha256);
    }
}
