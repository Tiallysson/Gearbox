using System.Text;
using System.Text.RegularExpressions;

namespace Gearbox.Modules.Notch.Models;

public sealed record BoltAppearance(BoltMood Mood, string PhraseKey);

public sealed partial class BoltScheduler(Func<double> random)
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(8);
    public static readonly TimeSpan MaxInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SleepAfter = TimeSpan.FromMinutes(15);
    public const int PreviewLength = 60;

    private static readonly BoltMood[] s_awakeMoods = [BoltMood.Happy, BoltMood.EarTwitch, BoltMood.Yawn];

    private static readonly Dictionary<BoltMood, int> s_phraseCount = new()
    {
        [BoltMood.Happy] = 4,
        [BoltMood.EarTwitch] = 3,
        [BoltMood.Yawn] = 3,
        [BoltMood.Sleeping] = 2,
    };

    public TimeSpan NextDelay() => MinInterval + ((MaxInterval - MinInterval) * Math.Clamp(random(), 0, 1));

    public BoltAppearance Choose(DateTimeOffset now, DateTimeOffset lastInteraction)
    {
        var mood = now - lastInteraction >= SleepAfter ? BoltMood.Sleeping : Pick(s_awakeMoods);
        var count = s_phraseCount[mood];
        var index = Math.Min(count - 1, (int)(Math.Clamp(random(), 0, 1) * count)) + 1;
        return new BoltAppearance(mood, $"BoltPhrase{mood}{index}");
    }

    public static bool CanAppear(NotchMode mode, PeekKind peek, NotchVisibility visibility, bool sessionLocked, bool displayOff) =>
        mode == NotchMode.Collapsed &&
        peek == PeekKind.None &&
        visibility == NotchVisibility.Normal &&
        !sessionLocked &&
        !displayOff;

    public static string Preview(string reply)
    {
        var line = reply
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(l => MarkdownMarkers().Replace(l, string.Empty).Trim())
            .FirstOrDefault(l => l.Length > 0 && !l.StartsWith("```", StringComparison.Ordinal)) ?? string.Empty;

        var flat = new StringBuilder(line.Length);
        foreach (var character in line)
        {
            if (character is not ('*' or '`' or '_'))
            {
                flat.Append(character);
            }
        }

        var text = flat.ToString().Trim();
        return text.Length <= PreviewLength ? text : text[..(PreviewLength - 1)].TrimEnd() + "…";
    }

    private BoltMood Pick(BoltMood[] moods) =>
        moods[Math.Min(moods.Length - 1, (int)(Math.Clamp(random(), 0, 1) * moods.Length))];

    [GeneratedRegex(@"^\s*(#{1,6}\s+|[-*+]\s+|\d{1,3}[.)]\s+|>\s*)")]
    private static partial Regex MarkdownMarkers();
}
