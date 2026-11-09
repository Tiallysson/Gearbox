using Gearbox.Interop.Audio;

namespace Gearbox.Modules.Notch.Logic;

public sealed record MixerAppPreference(float? Volume = null, bool? Muted = null, string? DeviceId = null);

public readonly record struct SessionRestore(string InstanceId, float? Volume, bool? Muted);

public readonly record struct AppRoute(uint ProcessId, string? DeviceId);

public sealed record MixerResetPlan(List<string> RestoreSessions, List<uint> UnrouteProcesses);

public static class MixerMemory
{
    private const float VolumeTolerance = 0.005f;

    public static Dictionary<string, MixerAppPreference> With(
        IReadOnlyDictionary<string, MixerAppPreference> preferences,
        string key,
        Func<MixerAppPreference, MixerAppPreference> change)
    {
        var next = new Dictionary<string, MixerAppPreference>(preferences, StringComparer.Ordinal);
        next[key] = change(next.TryGetValue(key, out var current) ? current : new MixerAppPreference());
        return next;
    }

    public static Dictionary<string, MixerAppPreference> Valid(IReadOnlyDictionary<string, MixerAppPreference?>? stored) =>
        (stored ?? new Dictionary<string, MixerAppPreference?>())
            .Where(pair => !string.IsNullOrEmpty(pair.Key) && MixerApps.IsPersistable(pair.Key) && pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.Ordinal);

    public static List<AudioSessionInfo> Fresh(IEnumerable<AudioSessionInfo> sessions, IReadOnlyDictionary<string, string> known) =>
        sessions.Where(session => !known.ContainsKey(session.InstanceId)).ToList();

    public static Dictionary<string, string> Known(
        IReadOnlyDictionary<string, string> previous,
        IEnumerable<AudioSessionInfo> sessions,
        IReadOnlySet<string> unreadDevices)
    {
        var next = previous
            .Where(pair => unreadDevices.Contains(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var session in sessions)
        {
            next[session.InstanceId] = session.DeviceId;
        }

        return next;
    }

    public static List<SessionRestore> VolumePlan(
        IEnumerable<AudioSessionInfo> fresh,
        IReadOnlyDictionary<string, MixerAppPreference> preferences)
    {
        var plan = new List<SessionRestore>();
        foreach (var session in fresh)
        {
            var key = MixerApps.KeyFor(session);
            if (!MixerApps.IsPersistable(key) || !preferences.TryGetValue(key, out var preference))
            {
                continue;
            }

            float? volume = preference.Volume is { } level && Math.Abs(level - session.Volume) > VolumeTolerance
                ? Math.Clamp(level, 0f, 1f)
                : null;
            bool? muted = preference.Muted is { } mute && mute != session.IsMuted ? mute : null;
            if (volume is not null || muted is not null)
            {
                plan.Add(new SessionRestore(session.InstanceId, volume, muted));
            }
        }

        return plan;
    }

    public static List<AppRoute> RoutePlan(
        IEnumerable<AudioSessionInfo> fresh,
        IReadOnlyDictionary<string, MixerAppPreference> preferences,
        IReadOnlySet<string> availableDevices)
    {
        var plan = new List<AppRoute>();
        var planned = new HashSet<uint>();
        foreach (var session in fresh)
        {
            var key = MixerApps.KeyFor(session);
            if (session.ProcessId == 0 ||
                !MixerApps.IsPersistable(key) ||
                !preferences.TryGetValue(key, out var preference) ||
                preference.DeviceId is not { } device ||
                !availableDevices.Contains(device) ||
                session.DeviceId == device ||
                !planned.Add(session.ProcessId))
            {
                continue;
            }

            plan.Add(new AppRoute(session.ProcessId, device));
        }

        return plan;
    }

    public static List<AppRoute> RouteApp(IEnumerable<AudioSessionInfo> sessions, string key, string? deviceId) =>
        MixerApps.SessionsOf(sessions, key)
            .Where(session => session.ProcessId != 0)
            .Select(session => session.ProcessId)
            .Distinct()
            .Select(processId => new AppRoute(processId, deviceId))
            .ToList();

    public static MixerResetPlan Reset(
        IEnumerable<AudioSessionInfo> sessions,
        IReadOnlySet<string> volumeKeys,
        IReadOnlySet<string> routeKeys)
    {
        var restore = new List<string>();
        var unroute = new List<uint>();
        foreach (var session in sessions)
        {
            var key = MixerApps.KeyFor(session);
            if (volumeKeys.Contains(key))
            {
                restore.Add(session.InstanceId);
            }

            if (routeKeys.Contains(key) && session.ProcessId != 0 && !unroute.Contains(session.ProcessId))
            {
                unroute.Add(session.ProcessId);
            }
        }

        return new MixerResetPlan(restore, unroute);
    }
}
