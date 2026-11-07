using System.Text.Json;

namespace Gearbox.Core.Updates;

internal static class GitHubReleaseParser
{
    private const string Sha256Prefix = "sha256:";

    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static IReadOnlyList<ReleaseInfo> Parse(string json)
    {
        var releases = new List<ReleaseInfo>();
        foreach (var dto in JsonSerializer.Deserialize<List<ReleaseDto>>(json, s_options) ?? [])
        {
            if (dto.TagName is null || dto.HtmlUrl is null || !AppVersion.TryParse(dto.TagName, out var version))
            {
                continue;
            }

            var assets = (dto.Assets ?? []).Select(ToAsset).OfType<ReleaseAsset>().ToList();
            releases.Add(new ReleaseInfo(dto.TagName, version, dto.Prerelease, dto.Draft, dto.HtmlUrl, assets));
        }

        return releases;
    }

    private static ReleaseAsset? ToAsset(AssetDto dto) =>
        dto.Name is null || dto.BrowserDownloadUrl is null
            ? null
            : new ReleaseAsset(dto.Name, dto.BrowserDownloadUrl, dto.Size, ParseSha256(dto.Digest));

    private static string? ParseSha256(string? digest)
    {
        if (digest is null || !digest.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hex = digest[Sha256Prefix.Length..];
        return hex.Length == 64 && hex.All(char.IsAsciiHexDigit) ? hex.ToLowerInvariant() : null;
    }

    private sealed record ReleaseDto(string? TagName, Uri? HtmlUrl, bool Prerelease, bool Draft, List<AssetDto>? Assets);

    private sealed record AssetDto(string? Name, Uri? BrowserDownloadUrl, long Size, string? Digest);
}
