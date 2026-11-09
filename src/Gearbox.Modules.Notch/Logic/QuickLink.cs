namespace Gearbox.Modules.Notch.Logic;

public enum QuickLinkKind
{
    Web,
    App,
}

public sealed record QuickLink(string Id, QuickLinkKind Kind, string Name, string Target)
{
    public static QuickLink Create(QuickLinkKind kind, string name, string target) =>
        new(Guid.NewGuid().ToString("N"), kind, name, target);

    public static QuickLink ForInstalledApp(string name, string parsingName) =>
        Uri.TryCreate(parsingName, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? Create(QuickLinkKind.Web, name, uri.AbsoluteUri)
            : Create(QuickLinkKind.App, name, parsingName);

    public bool SameTarget(QuickLink other) =>
        Kind == other.Kind && string.Equals(Target, other.Target, StringComparison.OrdinalIgnoreCase);

    public static bool IsSafeId(string? id) => Guid.TryParseExact(id, "N", out _);

    public static bool TryGetWebTarget(string? target, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(target, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static bool IsSafeAppTarget(string? target) =>
        !string.IsNullOrWhiteSpace(target) &&
        !target.Any(char.IsControl) &&
        !target.Contains('"', StringComparison.Ordinal) &&
        !target.Split('\\', '/').Any(segment => segment == "..");

    public bool IsSafe() =>
        IsSafeId(Id) &&
        Kind switch
        {
            QuickLinkKind.Web => TryGetWebTarget(Target, out _),
            QuickLinkKind.App => IsSafeAppTarget(Target),
            _ => false,
        };
}

public static class QuickLinkList
{
    public const int MaxLinks = 30;

    public static List<QuickLink> Add(IReadOnlyList<QuickLink> links, QuickLink link)
    {
        var result = links.ToList();
        if (result.Count < MaxLinks && !result.Any(l => l.SameTarget(link)))
        {
            result.Add(link);
        }

        return result;
    }

    public static List<QuickLink> Safe(IEnumerable<QuickLink?>? links) =>
        (links ?? []).OfType<QuickLink>().Where(link => link.IsSafe()).Take(MaxLinks).ToList();

    public static List<QuickLink> Remove(IReadOnlyList<QuickLink> links, string id) =>
        links.Where(l => l.Id != id).ToList();

    public static List<QuickLink> Move(IReadOnlyList<QuickLink> links, string id, int offset)
    {
        var result = links.ToList();
        var index = result.FindIndex(l => l.Id == id);
        if (index < 0)
        {
            return result;
        }

        var target = Math.Clamp(index + offset, 0, result.Count - 1);
        var link = result[index];
        result.RemoveAt(index);
        result.Insert(target, link);
        return result;
    }
}
