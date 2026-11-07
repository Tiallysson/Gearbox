using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Gearbox.Modules.Notch.Logic;

public static partial class FaviconParser
{
    private const int MaxCandidates = 6;
    private const int AppleTouchIconSize = 180;

    public static IReadOnlyList<Uri> Candidates(string? html, Uri page)
    {
        var scored = new List<(Uri Uri, int Score)>();
        foreach (Match tag in LinkTag().Matches(html ?? string.Empty))
        {
            var attributes = Attributes(tag.Value);
            var rel = attributes.GetValueOrDefault("rel")?.ToLowerInvariant();
            var href = attributes.GetValueOrDefault("href");
            if (rel is null || string.IsNullOrWhiteSpace(href) || !rel.Contains("icon", StringComparison.Ordinal))
            {
                continue;
            }

            var type = attributes.GetValueOrDefault("type")?.ToLowerInvariant();
            if (type == "image/svg+xml" ||
                href.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                !Uri.TryCreate(page, WebUtility.HtmlDecode(href.Trim()), out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                continue;
            }

            var size = LargestSize(attributes.GetValueOrDefault("sizes"));
            var isAppleTouch = rel.Contains("apple-touch-icon", StringComparison.Ordinal);
            scored.Add((uri, isAppleTouch ? Math.Max(size, AppleTouchIconSize) + 1 : size));
        }

        var result = scored
            .OrderByDescending(c => c.Score)
            .Select(c => c.Uri)
            .ToList();
        result.Add(new Uri(page, "/apple-touch-icon.png"));
        result.Add(new Uri(page, "/favicon.ico"));
        return result.Distinct().Take(MaxCandidates).ToList();
    }

    private static int LargestSize(string? sizes)
    {
        var largest = 16;
        foreach (var part in (sizes ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var x = part.IndexOf('x', StringComparison.OrdinalIgnoreCase);
            if (x > 0 && int.TryParse(part[..x], NumberStyles.None, CultureInfo.InvariantCulture, out var width))
            {
                largest = Math.Max(largest, width);
            }
        }

        return largest;
    }

    private static Dictionary<string, string> Attributes(string tag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Attribute().Matches(tag))
        {
            attributes.TryAdd(match.Groups["n"].Value, match.Groups["v"].Value);
        }

        return attributes;
    }

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTag();

    [GeneratedRegex(@"(?<n>[a-zA-Z-]+)\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)'|(?<v>[^\s>""']+))")]
    private static partial Regex Attribute();
}
