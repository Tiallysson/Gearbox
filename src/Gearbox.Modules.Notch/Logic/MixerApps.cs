using Gearbox.Interop.Audio;

namespace Gearbox.Modules.Notch.Logic;

public sealed record MixerApp(
    string Key,
    string Name,
    string? ExecutablePath,
    float Volume,
    bool IsMuted,
    bool IsSystemSounds,
    string? OutputDeviceId);

public static class MixerApps
{
    public const string SystemSoundsKey = "#system";

    public const double MeterDecayPerTick = 0.08;

    private const string ProcessKeyPrefix = "pid:";

    public static string KeyFor(AudioSessionInfo session)
    {
        if (session.IsSystemSounds)
        {
            return SystemSoundsKey;
        }

        var file = Path.GetFileName(session.ExecutablePath);
        return string.IsNullOrEmpty(file) ? ProcessKeyPrefix + session.ProcessId : file.ToLowerInvariant();
    }

    public static bool IsPersistable(string key) => !key.StartsWith(ProcessKeyPrefix, StringComparison.Ordinal);

    public static string FallbackName(AudioSessionInfo session)
    {
        if (!string.IsNullOrWhiteSpace(session.DisplayName) && !session.DisplayName.StartsWith('@'))
        {
            return session.DisplayName;
        }

        var file = Path.GetFileNameWithoutExtension(session.ExecutablePath);
        return string.IsNullOrEmpty(file) ? session.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) : file;
    }

    public static List<MixerApp> Open(
        IEnumerable<AudioSessionInfo> sessions,
        Func<AudioSessionInfo, string> nameOf,
        IReadOnlyDictionary<string, MixerAppPreference> preferences) =>
        sessions
            .Where(session => !session.IsExpired)
            .GroupBy(KeyFor)
            .Select(group =>
            {
                var first = group
                    .OrderByDescending(session => session.IsActive)
                    .ThenBy(session => session.InstanceId, StringComparer.Ordinal)
                    .First();
                return new MixerApp(
                    group.Key,
                    nameOf(first),
                    first.ExecutablePath,
                    first.Volume,
                    group.All(session => session.IsMuted),
                    first.IsSystemSounds,
                    preferences.TryGetValue(group.Key, out var preference) ? preference.DeviceId : null);
            })
            .OrderByDescending(app => app.IsSystemSounds)
            .ThenBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(app => app.Key, StringComparer.Ordinal)
            .ToList();

    public static List<AudioSessionInfo> SessionsOf(IEnumerable<AudioSessionInfo> sessions, string key) =>
        sessions.Where(session => KeyFor(session) == key).ToList();

    public static double Meter(double previous, float peak)
    {
        var current = float.IsFinite(peak) ? Math.Clamp(peak, 0f, 1f) : 0;
        return Math.Max(current, Math.Max(0, previous - MeterDecayPerTick));
    }
}
