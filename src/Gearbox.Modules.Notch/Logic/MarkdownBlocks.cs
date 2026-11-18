using System.Text;
using System.Text.RegularExpressions;

namespace Gearbox.Modules.Notch.Logic;

[Flags]
public enum InlineStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Code = 4,
}

public sealed record InlineRun(string Text, InlineStyle Style);

public enum MarkdownBlockKind
{
    Paragraph,
    Heading,
    Bullet,
    Numbered,
    Code,
}

public sealed record MarkdownBlock(MarkdownBlockKind Kind, IReadOnlyList<InlineRun> Runs, string Marker = "")
{
    public string Text => string.Concat(Runs.Select(r => r.Text));
}

public static partial class MarkdownBlocks
{
    private const string Fence = "```";

    public static List<MarkdownBlock> Parse(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        var paragraph = new List<string>();
        StringBuilder? code = null;

        foreach (var raw in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith(Fence, StringComparison.Ordinal))
            {
                FlushParagraph();
                if (code is null)
                {
                    code = new StringBuilder();
                }
                else
                {
                    blocks.Add(CodeBlock(code));
                    code = null;
                }

                continue;
            }

            if (code is not null)
            {
                code.Append(raw).Append('\n');
                continue;
            }

            if (line.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (HeadingPattern().Match(line) is { Success: true } heading)
            {
                FlushParagraph();
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Heading, Inline(heading.Groups[2].Value, InlineStyle.Bold)));
            }
            else if (BulletPattern().Match(line) is { Success: true } bullet)
            {
                FlushParagraph();
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Bullet, Inline(bullet.Groups[1].Value), "•"));
            }
            else if (NumberedPattern().Match(line) is { Success: true } numbered)
            {
                FlushParagraph();
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Numbered, Inline(numbered.Groups[2].Value), numbered.Groups[1].Value + "."));
            }
            else
            {
                paragraph.Add(line.Trim());
            }
        }

        FlushParagraph();
        if (code is not null)
        {
            blocks.Add(CodeBlock(code));
        }

        return blocks;

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, Inline(string.Join('\n', paragraph))));
            paragraph.Clear();
        }

        static MarkdownBlock CodeBlock(StringBuilder code) =>
            new(MarkdownBlockKind.Code, (InlineRun[])[new InlineRun(code.ToString().TrimEnd('\n'), InlineStyle.Code)]);
    }

    public static List<InlineRun> Inline(string text, InlineStyle baseStyle = InlineStyle.None)
    {
        var runs = new List<InlineRun>();
        var current = new StringBuilder();
        var style = baseStyle;
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '`' && text.IndexOf('`', index + 1) is var close and > 0)
            {
                Flush();
                runs.Add(new InlineRun(text[(index + 1)..close], style | InlineStyle.Code));
                index = close + 1;
                continue;
            }

            if (string.CompareOrdinal(text, index, "**", 0, 2) == 0 &&
                ((style & InlineStyle.Bold) != 0 || text.IndexOf("**", index + 2, StringComparison.Ordinal) > index + 2))
            {
                Flush();
                style ^= InlineStyle.Bold;
                index += 2;
                continue;
            }

            if (text[index] == '*' && OpensOrClosesItalic(text, index, style))
            {
                Flush();
                style ^= InlineStyle.Italic;
                index++;
                continue;
            }

            current.Append(text[index]);
            index++;
        }

        Flush();
        return runs;

        void Flush()
        {
            if (current.Length > 0)
            {
                runs.Add(new InlineRun(current.ToString(), style));
                current.Clear();
            }
        }
    }

    private static bool OpensOrClosesItalic(string text, int index, InlineStyle style)
    {
        if ((style & InlineStyle.Italic) != 0)
        {
            return index > 0 && !char.IsWhiteSpace(text[index - 1]);
        }

        var next = index + 1;
        return next < text.Length && !char.IsWhiteSpace(text[next]) && text[next] != '*' &&
            text.IndexOf('*', next + 1) > next;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^\s*[-*+]\s+(.+)$")]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"^\s*(\d{1,3})[.)]\s+(.+)$")]
    private static partial Regex NumberedPattern();
}
