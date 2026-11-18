using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class OpenRouterOAuthTests
{
    [Fact]
    public void UrlDeAutorizacao_LevaCallbackDesafioEstadoELimite()
    {
        var uri = OpenRouterOAuth.AuthorizeUri("http://localhost:51423/callback", "abc-_", "st4te", 12.5m);
        var query = uri.Query;

        Assert.Equal("https://openrouter.ai/auth", uri.GetLeftPart(UriPartial.Path));
        Assert.Contains("callback_url=http%3A%2F%2Flocalhost%3A51423%2Fcallback", query, StringComparison.Ordinal);
        Assert.Contains("code_challenge=abc-_", query, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", query, StringComparison.Ordinal);
        Assert.Contains("state=st4te", query, StringComparison.Ordinal);
        Assert.Contains("key_label=Gearbox", query, StringComparison.Ordinal);
        Assert.Contains("limit=12.5", query, StringComparison.Ordinal);
    }

    [Fact]
    public void UrlDeAutorizacao_SemLimite_NaoMandaLimit()
    {
        var uri = OpenRouterOAuth.AuthorizeUri("http://localhost:1/callback", "c", "s", 0m);

        Assert.DoesNotContain("limit=", uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public void CallbackUrl_UsaLocalhostEAPorta()
    {
        Assert.Equal("http://localhost:51423/callback", OpenRouterOAuth.CallbackUrl(51423));
    }

    [Fact]
    public void LinhaDoCallback_ExtraiCodigoEEstado()
    {
        var callback = OpenRouterOAuth.ParseRequestLine("GET /callback?code=a%2Bb&state=xyz HTTP/1.1");

        Assert.NotNull(callback);
        Assert.Equal("a+b", callback.Code);
        Assert.Equal("xyz", callback.State);
        Assert.True(callback.IsApprovedFor("xyz"));
        Assert.False(callback.IsApprovedFor("outro"));
    }

    [Fact]
    public void LinhaDoCallback_ComErro_NaoEAprovada()
    {
        var callback = OpenRouterOAuth.ParseRequestLine("GET /callback?error=access_denied&state=xyz HTTP/1.1");

        Assert.NotNull(callback);
        Assert.Equal("access_denied", callback.Error);
        Assert.False(callback.IsApprovedFor("xyz"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GET /favicon.ico HTTP/1.1")]
    [InlineData("POST /callback?code=a&state=b HTTP/1.1")]
    [InlineData("GET /callbackx?code=a HTTP/1.1")]
    public void OutrasRequisicoes_SaoIgnoradas(string? line)
    {
        Assert.Null(OpenRouterOAuth.ParseRequestLine(line));
    }

    [Fact]
    public void CallbackSemCodigo_NaoEAprovado()
    {
        var callback = OpenRouterOAuth.ParseRequestLine("GET /callback?state=xyz HTTP/1.1");

        Assert.NotNull(callback);
        Assert.False(callback.IsApprovedFor("xyz"));
    }
}
