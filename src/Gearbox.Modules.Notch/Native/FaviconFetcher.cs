using System.Runtime.InteropServices.WindowsRuntime;
using System.Net.Http.Headers;
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

    public static async Task<byte[]?> TryDownloadAsync(Uri page, CancellationToken cancellation)
    {
        var (html, baseUri) = await TryReadPageAsync(page, cancellation);
        foreach (var candidate in FaviconParser.Candidates(html, baseUri))
        {
            var icon = await TryReadAsync(candidate, MaxIconBytes, cancellation);
            if (icon is not null && await IsDecodableAsync(icon))
            {
                return icon;
            }
        }

        return null;
    }

    private static async Task<(string? Html, Uri BaseUri)> TryReadPageAsync(Uri page, CancellationToken cancellation)
    {
        try
        {
            using var response = await s_http.GetAsync(page, HttpCompletionOption.ResponseHeadersRead, cancellation);
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

    private static async Task<byte[]?> TryReadAsync(Uri uri, int limit, CancellationToken cancellation)
    {
        try
        {
            using var response = await s_http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation);
            return response.IsSuccessStatusCode ? await ReadLimitedAsync(response, limit, cancellation) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
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
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Gearbox");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
        return http;
    }
}
