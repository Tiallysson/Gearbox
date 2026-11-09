using System.Runtime.InteropServices.WindowsRuntime;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Gearbox.Modules.Notch.Logic;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Gearbox.Modules.Notch.Native;

internal static class FaviconFetcher
{
    private const int MaxPageBytes = 512 * 1024;
    private const int MaxIconBytes = 1024 * 1024;

    private static readonly HttpClient s_http = CreateClient();

    private const int MaxRedirects = 3;

    public static async Task<byte[]?> TryDownloadAsync(Uri page, CancellationToken cancellation)
    {
        var allowPrivate = !await IsPublicHostAsync(page, cancellation);
        var (html, baseUri) = await TryReadPageAsync(page, allowPrivate, cancellation);
        foreach (var candidate in FaviconParser.Candidates(html, baseUri))
        {
            var icon = await TryReadAsync(candidate, MaxIconBytes, allowPrivate, cancellation);
            if (icon is not null && await IsDecodableAsync(icon))
            {
                return icon;
            }
        }

        return null;
    }

    private static async Task<(string? Html, Uri BaseUri)> TryReadPageAsync(Uri page, bool allowPrivate, CancellationToken cancellation)
    {
        try
        {
            using var response = await GetCheckedAsync(page, allowPrivate, cancellation);
            if (response is null)
            {
                return (null, page);
            }

            var baseUri = response.RequestMessage?.RequestUri ?? page;
            if (!response.IsSuccessStatusCode)
            {
                return (null, baseUri);
            }

            var bytes = await ReadLimitedAsync(response, MaxPageBytes, cancellation);
            return (bytes is null ? null : Encoding.UTF8.GetString(bytes), baseUri);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return (null, page);
        }
    }

    private static async Task<byte[]?> TryReadAsync(Uri uri, int limit, bool allowPrivate, CancellationToken cancellation)
    {
        try
        {
            using var response = await GetCheckedAsync(uri, allowPrivate, cancellation);
            return response is { IsSuccessStatusCode: true } ? await ReadLimitedAsync(response, limit, cancellation) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    private static async Task<HttpResponseMessage?> GetCheckedAsync(Uri uri, bool allowPrivate, CancellationToken cancellation)
    {
        var current = uri;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!QuickLink.TryGetWebTarget(current.AbsoluteUri, out _) ||
                (!allowPrivate && !await IsPublicHostAsync(current, cancellation)))
            {
                return null;
            }

            var response = await s_http.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellation);
            var status = (int)response.StatusCode;
            if (status is < 300 or > 399 || response.Headers.Location is not { } location)
            {
                return response;
            }

            response.Dispose();
            var next = location.IsAbsoluteUri ? location : new Uri(current, location);
            if (current.Scheme == Uri.UriSchemeHttps && next.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            current = next;
        }

        return null;
    }

    private static async Task<bool> IsPublicHostAsync(Uri uri, CancellationToken cancellation)
    {
        if (IPAddress.TryParse(uri.IdnHost, out var literal))
        {
            return NetworkAddressRules.IsPublic(literal);
        }

        try
        {
            return NetworkAddressRules.AllPublic(await Dns.GetHostAddressesAsync(uri.IdnHost, cancellation));
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return false;
        }
    }

    private static async Task<byte[]?> ReadLimitedAsync(HttpResponseMessage response, int limit, CancellationToken cancellation)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellation)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length >= limit)
            {
                break;
            }
        }

        return buffer.Length == 0 ? null : buffer.ToArray();
    }

    private static async Task<bool> IsDecodableAsync(byte[] image)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(image.AsBuffer());
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            return decoder.PixelWidth > 0 && decoder.PixelHeight > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Gearbox");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
        return http;
    }
}
