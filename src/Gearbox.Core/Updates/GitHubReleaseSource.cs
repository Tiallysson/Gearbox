namespace Gearbox.Core.Updates;

internal sealed class GitHubReleaseSource(HttpClient http) : IReleaseSource
{
    private static readonly Uri s_releasesUri = new("https://api.github.com/repos/Tiallysson/Gearbox/releases?per_page=30");

    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(15);

    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(s_timeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, s_releasesUri);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        return GitHubReleaseParser.Parse(json);
    }
}
