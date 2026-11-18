using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Native;

internal sealed class AiUsageClient : IDisposable
{
    private const int MaxPages = 10;
    private const string AnthropicVersion = "2026-10-21";

    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("Gearbox", "1.0") } },
    };

    public void Dispose() => _http.Dispose();

    public async Task<(AiUsageStatus Status, decimal Spent)> FetchMonthToDateAsync(
        AiProvider provider,
        string key,
        DateTimeOffset monthStart,
        CancellationToken cancellation)
    {
        var total = 0m;
        string? page = null;
        for (var count = 0; count < MaxPages; count++)
        {
            using var request = BuildRequest(provider, key, monthStart, page);
            using var response = await _http.SendAsync(request, cancellation).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return (AiUsageStatus.InvalidKey, 0m);
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
            var parsed = AiCostReport.Parse(provider, json);
            total += parsed.Dollars;
            if (parsed.NextPage is null)
            {
                return (AiUsageStatus.Ok, total);
            }

            page = parsed.NextPage;
        }

        return (AiUsageStatus.Ok, total);
    }

    public static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or IOException or JsonException or TaskCanceledException;

    private static HttpRequestMessage BuildRequest(AiProvider provider, string key, DateTimeOffset monthStart, string? page)
    {
        var pageQuery = page is null ? string.Empty : "&page=" + Uri.EscapeDataString(page);
        HttpRequestMessage request;
        if (provider == AiProvider.Anthropic)
        {
            var start = Uri.EscapeDataString(monthStart.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.anthropic.com/v1/organizations/cost_report?starting_at={start}&bucket_width=1d&limit=31{pageQuery}");
            request.Headers.Add("x-api-key", key);
            request.Headers.Add("anthropic-version", AnthropicVersion);
        }
        else
        {
            var start = monthStart.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.openai.com/v1/organization/costs?start_time={start}&bucket_width=1d&limit=31{pageQuery}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        return request;
    }
}
