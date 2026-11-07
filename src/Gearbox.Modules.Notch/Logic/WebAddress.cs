namespace Gearbox.Modules.Notch.Logic;

public static class WebAddress
{
    public static bool TryNormalize(string? input, out Uri uri)
    {
        uri = null!;
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text) || text.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) ||
            !IsPlausibleHost(parsed.Host))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static string DefaultName(Uri uri)
    {
        var host = uri.Host;
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        return host;
    }

    private static bool IsPlausibleHost(string host) =>
        host.Length > 0 &&
        (host.Contains('.', StringComparison.Ordinal) || host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
}
