using System.Runtime.InteropServices;

namespace Gearbox.Core.Updates;

public static class UpdateSelector
{
    private const string InstallerPrefix = "Gearbox-";

    public static bool IncludesPrereleases(bool optedIn, AppVersion current) => optedIn || current.IsPrerelease;

    public static UpdateCandidate? Select(AppVersion current, IEnumerable<ReleaseInfo> releases, Architecture architecture, bool includePrereleases)
    {
        var installerSuffix = InstallerSuffix(architecture);
        if (installerSuffix is null)
        {
            return null;
        }

        UpdateCandidate? best = null;
        foreach (var release in releases)
        {
            if (!IsEligible(release, current, includePrereleases))
            {
                continue;
            }

            var installer = release.Assets.FirstOrDefault(asset => IsInstaller(asset, installerSuffix));
            if (installer is not null && (best is null || release.Version > best.Release.Version))
            {
                best = new UpdateCandidate(release, installer);
            }
        }

        return best;
    }

    private static bool IsEligible(ReleaseInfo release, AppVersion current, bool includePrereleases)
    {
        var isPrerelease = release.IsPrerelease || release.Version.IsPrerelease;
        return !release.IsDraft && (includePrereleases || !isPrerelease) && release.Version > current;
    }

    private static bool IsInstaller(ReleaseAsset asset, string suffix) =>
        asset.Sha256 is not null &&
        asset.DownloadUrl.Scheme == Uri.UriSchemeHttps &&
        asset.Name.StartsWith(InstallerPrefix, StringComparison.OrdinalIgnoreCase) &&
        asset.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    private static string? InstallerSuffix(Architecture architecture) => architecture switch
    {
        Architecture.X64 => "-x64-setup.exe",
        Architecture.Arm64 => "-arm64-setup.exe",
        _ => null,
    };
}
