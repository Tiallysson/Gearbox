using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Gearbox.Core.Updates;

public sealed class UpdateDownloader(HttpClient http, GearboxPaths paths, ILogger<UpdateDownloader> logger)
{
    public async Task<string> DownloadAsync(ReleaseAsset asset, CancellationToken cancellationToken)
    {
        var expectedHash = asset.Sha256 ?? throw new InvalidDataException($"{asset.Name} não tem SHA-256 publicado.");
        if (asset.DownloadUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException($"{asset.Name} não é servido por HTTPS.");
        }

        Directory.CreateDirectory(paths.UpdatesDirectory);
        var target = Path.Combine(paths.UpdatesDirectory, Path.GetFileName(asset.Name));
        DeleteOtherFiles(target);

        if (File.Exists(target) && await HashFileAsync(target, cancellationToken).ConfigureAwait(false) == expectedHash)
        {
            logger.LogInformation("Instalador {Asset} já baixado e verificado", asset.Name);
            return target;
        }

        var partial = target + ".part";
        try
        {
            using (var response = await http.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var destination = File.Create(partial);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            var actualHash = await HashFileAsync(partial, cancellationToken).ConfigureAwait(false);
            if (actualHash != expectedHash)
            {
                throw new InvalidDataException($"SHA-256 de {asset.Name} não confere: esperado {expectedHash}, obtido {actualHash}.");
            }

            File.Move(partial, target, overwrite: true);
            logger.LogInformation("Instalador {Asset} baixado e verificado ({Hash})", asset.Name, actualHash);
            return target;
        }
        finally
        {
            File.Delete(partial);
        }
    }

    private void DeleteOtherFiles(string keep)
    {
        foreach (var file in Directory.EnumerateFiles(paths.UpdatesDirectory))
        {
            if (!string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(file);
            }
        }
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }
}
