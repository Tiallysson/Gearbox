namespace Gearbox.Core.Updates;

public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size, string? Sha256);

public sealed record ReleaseInfo(string Tag, AppVersion Version, bool IsPrerelease, bool IsDraft, Uri HtmlUrl, IReadOnlyList<ReleaseAsset> Assets);

public sealed record UpdateCandidate(ReleaseInfo Release, ReleaseAsset Installer);
