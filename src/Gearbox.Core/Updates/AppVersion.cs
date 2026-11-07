using System.Globalization;

namespace Gearbox.Core.Updates;

public readonly record struct AppVersion(int Major, int Minor, int Patch, string Prerelease) : IComparable<AppVersion>
{
    public string Prerelease { get; init; } = Prerelease ?? string.Empty;

    public bool IsPrerelease => !string.IsNullOrEmpty(Prerelease);

    public static bool TryParse(string? text, out AppVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim().TrimStart('v', 'V');
        var buildStart = value.IndexOf('+', StringComparison.Ordinal);
        if (buildStart >= 0)
        {
            value = value[..buildStart];
        }

        var prerelease = string.Empty;
        var prereleaseStart = value.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseStart >= 0)
        {
            prerelease = value[(prereleaseStart + 1)..];
            value = value[..prereleaseStart];
            if (!IsValidPrerelease(prerelease))
            {
                return false;
            }
        }

        var parts = value.Split('.');
        if (parts.Length != 3 ||
            !TryParseNumber(parts[0], out var major) ||
            !TryParseNumber(parts[1], out var minor) ||
            !TryParseNumber(parts[2], out var patch))
        {
            return false;
        }

        version = new AppVersion(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(AppVersion other)
    {
        var core = Major.CompareTo(other.Major);
        if (core == 0)
        {
            core = Minor.CompareTo(other.Minor);
        }

        if (core == 0)
        {
            core = Patch.CompareTo(other.Patch);
        }

        return core != 0 ? core : ComparePrerelease(Prerelease, other.Prerelease);
    }

    public override string ToString() =>
        IsPrerelease ? $"{Major}.{Minor}.{Patch}-{Prerelease}" : $"{Major}.{Minor}.{Patch}";

    public static bool operator <(AppVersion left, AppVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(AppVersion left, AppVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(AppVersion left, AppVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(AppVersion left, AppVersion right) => left.CompareTo(right) >= 0;

    private static int ComparePrerelease(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length == 0 ? 0 : 1;
        }

        if (right.Length == 0)
        {
            return -1;
        }

        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        var identifierCount = Math.Min(leftParts.Length, rightParts.Length);

        for (var index = 0; index < identifierCount; index++)
        {
            var leftIdentifier = leftParts[index];
            var rightIdentifier = rightParts[index];
            var comparison = (leftIdentifier.All(char.IsAsciiDigit), rightIdentifier.All(char.IsAsciiDigit)) switch
            {
                (true, true) => CompareNumeric(leftIdentifier, rightIdentifier),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(leftIdentifier, rightIdentifier),
            };

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static int CompareNumeric(string left, string right)
    {
        var lengthComparison = left.Length.CompareTo(right.Length);
        return lengthComparison != 0 ? lengthComparison : string.CompareOrdinal(left, right);
    }

    private static bool TryParseNumber(string text, out int number) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    private static bool IsValidPrerelease(string prerelease) =>
        prerelease.Split('.').All(id => id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
}
