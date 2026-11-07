using System.Net;
using System.Security.Cryptography;
using System.Text;
using Gearbox.Core.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gearbox.Core.Tests.Updates;

public sealed class UpdateDownloaderTests : IDisposable
{
    private static readonly byte[] s_content = Encoding.UTF8.GetBytes("instalador falso");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "gearbox-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeHandler _handler = new(s_content);
    private readonly UpdateDownloader _downloader;

    public UpdateDownloaderTests()
    {
        _downloader = new UpdateDownloader(new HttpClient(_handler), new GearboxPaths(_root), NullLogger<UpdateDownloader>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_HashCerto_GravaNaPastaDeAtualizacoes()
    {
        var path = await _downloader.DownloadAsync(Asset(HashOf(s_content)), TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(_root, "updates", "Gearbox-0.4.0-x64-setup.exe"), path);
        Assert.Equal(s_content, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_HashErrado_FalhaENaoDeixaArquivo()
    {
        var asset = Asset(new string('0', 64));

        await Assert.ThrowsAsync<InvalidDataException>(() => _downloader.DownloadAsync(asset, TestContext.Current.CancellationToken));

        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "updates")));
    }

    [Fact]
    public async Task DownloadAsync_ArquivoJaVerificado_NaoBaixaDeNovo()
    {
        var asset = Asset(HashOf(s_content));

        await _downloader.DownloadAsync(asset, TestContext.Current.CancellationToken);
        await _downloader.DownloadAsync(asset, TestContext.Current.CancellationToken);

        Assert.Equal(1, _handler.Requests);
    }

    [Fact]
    public async Task DownloadAsync_ArquivoCorrompido_BaixaDeNovo()
    {
        var asset = Asset(HashOf(s_content));
        var path = await _downloader.DownloadAsync(asset, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, "adulterado", TestContext.Current.CancellationToken);

        await _downloader.DownloadAsync(asset, TestContext.Current.CancellationToken);

        Assert.Equal(2, _handler.Requests);
        Assert.Equal(s_content, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_ApagaInstaladoresAntigos()
    {
        var updates = Path.Combine(_root, "updates");
        Directory.CreateDirectory(updates);
        await File.WriteAllTextAsync(Path.Combine(updates, "Gearbox-0.3.0-x64-setup.exe"), "antigo", TestContext.Current.CancellationToken);

        await _downloader.DownloadAsync(Asset(HashOf(s_content)), TestContext.Current.CancellationToken);

        Assert.Equal(["Gearbox-0.4.0-x64-setup.exe"], Directory.EnumerateFiles(updates).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("http://github.com/a.exe", "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899")]
    [InlineData("https://github.com/a.exe", null)]
    public async Task DownloadAsync_SemHttpsOuSemHash_RecusaSemBaixar(string url, string? hash)
    {
        var asset = new ReleaseAsset("Gearbox-0.4.0-x64-setup.exe", new Uri(url), 1, hash);

        await Assert.ThrowsAsync<InvalidDataException>(() => _downloader.DownloadAsync(asset, TestContext.Current.CancellationToken));

        Assert.Equal(0, _handler.Requests);
    }

    private static ReleaseAsset Asset(string sha256) =>
        new("Gearbox-0.4.0-x64-setup.exe", new Uri("https://github.com/Tiallysson/Gearbox/releases/download/v0.4.0/Gearbox-0.4.0-x64-setup.exe"), s_content.Length, sha256);

    private static string HashOf(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private sealed class FakeHandler(byte[] content) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        }
    }
}
