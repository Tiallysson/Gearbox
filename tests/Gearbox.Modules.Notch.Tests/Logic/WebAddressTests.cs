using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class WebAddressTests
{
    [Theory]
    [InlineData("youtube.com", "https://youtube.com/")]
    [InlineData("  https://mail.google.com/u/0  ", "https://mail.google.com/u/0")]
    [InlineData("http://exemplo.com.br/a?b=1", "http://exemplo.com.br/a?b=1")]
    [InlineData("localhost:8080", "https://localhost:8080/")]
    public void TryNormalize_AceitaEnderecosComOuSemProtocolo(string input, string expected)
    {
        Assert.True(WebAddress.TryNormalize(input, out var uri));
        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("não é site")]
    [InlineData("semponto")]
    [InlineData("ftp://exemplo.com")]
    [InlineData("file:///C:/Windows")]
    [InlineData("javascript:alert(1)")]
    public void TryNormalize_RecusaOQueNaoESite(string? input)
    {
        Assert.False(WebAddress.TryNormalize(input, out _));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch", "youtube.com")]
    [InlineData("https://mail.google.com/", "mail.google.com")]
    public void DefaultName_UsaOHostSemWww(string address, string expected)
    {
        Assert.Equal(expected, WebAddress.DefaultName(new Uri(address)));
    }
}
