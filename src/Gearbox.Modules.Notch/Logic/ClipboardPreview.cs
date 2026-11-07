using System.Text;

namespace Gearbox.Modules.Notch.Logic;

public static class ClipboardPreview
{
    public const int MaxLength = 120;

    public static IReadOnlyList<string> FilePaths(string? joinedPaths) =>
        string.IsNullOrEmpty(joinedPaths) ? [] : joinedPaths.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    public static string FileName(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    public static string? ForText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var builder = new StringBuilder(Math.Min(text.Length, MaxLength + 1));
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
            if (builder.Length > MaxLength)
            {
                return string.Concat(builder.ToString(0, MaxLength).TrimEnd(), "…");
            }
        }

        return builder.ToString();
    }
}
