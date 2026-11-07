using System.Reflection;

namespace Gearbox.Services;

internal static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString()
        ?? "?";
}
