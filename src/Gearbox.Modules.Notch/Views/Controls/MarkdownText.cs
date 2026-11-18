using Gearbox.Modules.Notch.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Gearbox.Modules.Notch.Views.Controls;

public static class MarkdownText
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.RegisterAttached(
        "Markdown",
        typeof(string),
        typeof(MarkdownText),
        new PropertyMetadata(string.Empty, (d, e) => Render((RichTextBlock)d, e.NewValue as string ?? string.Empty)));

    private static readonly FontFamily s_monospace = new("Cascadia Mono, Consolas");
    private static readonly SolidColorBrush s_codeBackground = new(Color.FromArgb(0x26, 0x80, 0x80, 0x80));

    public static string GetMarkdown(RichTextBlock element) => (string)element.GetValue(MarkdownProperty);

    public static void SetMarkdown(RichTextBlock element, string value) => element.SetValue(MarkdownProperty, value);

    private static void Render(RichTextBlock target, string markdown)
    {
        target.Blocks.Clear();
        foreach (var block in MarkdownBlocks.Parse(markdown))
        {
            target.Blocks.Add(block.Kind == MarkdownBlockKind.Code ? CodeParagraph(block.Text) : TextParagraph(block));
        }
    }

    private static Paragraph TextParagraph(MarkdownBlock block)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
                paragraph.FontSize = 14;
                break;
            case MarkdownBlockKind.Bullet or MarkdownBlockKind.Numbered:
                paragraph.Margin = new Thickness(8, 0, 0, 2);
                paragraph.Inlines.Add(new Run { Text = block.Marker + " " });
                break;
        }

        foreach (var run in block.Runs)
        {
            AddRun(paragraph.Inlines, run);
        }

        return paragraph;
    }

    private static void AddRun(InlineCollection inlines, InlineRun run)
    {
        var lines = run.Text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                inlines.Add(new LineBreak());
            }

            if (lines[index].Length == 0)
            {
                continue;
            }

            var text = new Run { Text = lines[index] };
            if (run.Style.HasFlag(InlineStyle.Bold))
            {
                text.FontWeight = FontWeights.SemiBold;
            }

            if (run.Style.HasFlag(InlineStyle.Italic))
            {
                text.FontStyle = Windows.UI.Text.FontStyle.Italic;
            }

            if (run.Style.HasFlag(InlineStyle.Code))
            {
                text.FontFamily = s_monospace;
            }

            inlines.Add(text);
        }
    }

    private static Paragraph CodeParagraph(string code)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 8) };
        paragraph.Inlines.Add(new InlineUIContainer
        {
            Child = new Border
            {
                Background = s_codeBackground,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Child = new TextBlock
                {
                    Text = code,
                    FontFamily = s_monospace,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                },
            },
        });
        return paragraph;
    }
}
