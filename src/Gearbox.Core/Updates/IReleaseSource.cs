namespace Gearbox.Core.Updates;

public interface IReleaseSource
{
    Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken cancellationToken);
}
