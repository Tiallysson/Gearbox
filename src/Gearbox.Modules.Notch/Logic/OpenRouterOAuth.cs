using System.Globalization;

namespace Gearbox.Modules.Notch.Logic;

public sealed record OAuthCallback(string? Code, string? State, string? Error)
{
    public bool IsApprovedFor(string expectedState) =>
        Error is null && !string.IsNullOrEmpty(Code) && string.Equals(State, expectedState, StringComparison.Ordinal);
}

public static class OpenRouterOAuth
{
    public const string CallbackPath = "/callback";
    public const string KeyLabel = "Gearbox";

    private const string AuthorizeEndpoint = "https://openrouter.ai/auth";

    public static string CallbackUrl(int port) => $"http://localhost:{port.ToString(CultureInfo.InvariantCulture)}{CallbackPath}";

    public static Uri AuthorizeUri(string callbackUrl, string challenge, string state, decimal creditLimit)
    {
        var query = new List<string>
        {
            "callback_url=" + Uri.EscapeDataString(callbackUrl),
            "code_challenge=" + Uri.EscapeDataString(challenge),
            "code_challenge_method=" + Pkce.Method,
            "state=" + Uri.EscapeDataString(state),
            "key_label=" + Uri.EscapeDataString(KeyLabel),
        };

        if (creditLimit > 0)
        {
            query.Add("limit=" + creditLimit.ToString("0.##", CultureInfo.InvariantCulture));
        }

        return new Uri(AuthorizeEndpoint + "?" + string.Join('&', query));
    }

    public static OAuthCallback? ParseRequestLine(string? requestLine)
    {
        var parts = requestLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: >= 2 } || parts[0] != "GET")
        {
            return null;
        }

        var target = parts[1];
        var queryStart = target.IndexOf('?', StringComparison.Ordinal);
        var path = queryStart < 0 ? target : target[..queryStart];
        if (!string.Equals(path, CallbackPath, StringComparison.Ordinal))
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (queryStart >= 0)
        {
            foreach (var pair in target[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=', StringComparison.Ordinal);
                var name = Decode(separator < 0 ? pair : pair[..separator]);
                var value = separator < 0 ? string.Empty : Decode(pair[(separator + 1)..]);
                values.TryAdd(name, value);
            }
        }

        return new OAuthCallback(
            values.GetValueOrDefault("code"),
            values.GetValueOrDefault("state"),
            values.TryGetValue("error", out var error) ? error : null);

        static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
    }
}
