using Gearbox.Modules.Wallpaper.Logic;

namespace Gearbox.Modules.Wallpaper.Tests;

public sealed class MediaKindTests
{
    [Theory]
    [InlineData(@"C:\w\praia.jpg", MediaKind.Image)]
    [InlineData(@"C:\w\praia.JPEG", MediaKind.Image)]
    [InlineData(@"C:\w\praia.png", MediaKind.Image)]
    [InlineData(@"C:\w\praia.webp", MediaKind.Image)]
    [InlineData(@"C:\w\loop.mp4", MediaKind.Video)]
    [InlineData(@"C:\w\loop.WebM", MediaKind.Video)]
    [InlineData(@"C:\w\loop.mkv", MediaKind.Video)]
    [InlineData(@"C:\w\anim.gif", MediaKind.Unsupported)]
    [InlineData(@"C:\w\sem-extensao", MediaKind.Unsupported)]
    [InlineData("", MediaKind.Unsupported)]
    [InlineData(null, MediaKind.Unsupported)]
    public void Detect_PelaExtensao(string? path, MediaKind expected)
    {
        Assert.Equal(expected, MediaKinds.Detect(path));
    }
}
