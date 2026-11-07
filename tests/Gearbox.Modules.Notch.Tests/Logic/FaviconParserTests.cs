using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class FaviconParserTests
{
    private static readonly Uri s_page = new("https://exemplo.com/pasta/pagina");

    [Fact]
    public void Candidates_SemTags_UsaOsCaminhosPadrao()
    {
        var candidates = FaviconParser.Candidates("<html></html>", s_page);

        Assert.Equal(
            [new Uri("https://exemplo.com/apple-touch-icon.png"), new Uri("https://exemplo.com/favicon.ico")],
            candidates);
    }

    [Fact]
    public void Candidates_PreferemOMaiorIcone()
    {
        const string html = """
            <link rel="icon" href="/16.png" sizes="16x16">
            <link rel="icon" type="image/png" href="/192.png" sizes="192x192">
            <link rel="apple-touch-icon" href="/apple.png">
            """;

        var candidates = FaviconParser.Candidates(html, s_page);

        Assert.Equal(new Uri("https://exemplo.com/192.png"), candidates[0]);
        Assert.Equal(new Uri("https://exemplo.com/apple.png"), candidates[1]);
        Assert.Equal(new Uri("https://exemplo.com/16.png"), candidates[2]);
    }

    [Fact]
    public void Candidates_ResolveCaminhosRelativosEEntidades()
    {
        const string html = "<LINK REL='shortcut icon' HREF='img/fav.ico?v=1&amp;x=2'>";

        var candidates = FaviconParser.Candidates(html, s_page);

        Assert.Equal(new Uri("https://exemplo.com/pasta/img/fav.ico?v=1&x=2"), candidates[0]);
    }

    [Fact]
    public void Candidates_IgnoramSvgEOutrosRels()
    {
        const string html = """
            <link rel="icon" type="image/svg+xml" href="/icon.svg">
            <link rel="mask-icon" href="/mask.svg">
            <link rel="stylesheet" href="/site.css">
            <link rel="icon" href="data:image/png;base64,AAAA">
            """;

        var candidates = FaviconParser.Candidates(html, s_page);

        Assert.DoesNotContain(candidates, c => c.AbsolutePath.EndsWith(".svg", StringComparison.Ordinal));
        Assert.DoesNotContain(candidates, c => c.AbsolutePath.EndsWith(".css", StringComparison.Ordinal));
        Assert.DoesNotContain(candidates, c => c.Scheme == "data");
        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public void Candidates_SemRepetirELimitados()
    {
        var html = string.Concat(Enumerable.Range(0, 20).Select(i => $"<link rel=\"icon\" href=\"/i{i}.png\">")) +
            "<link rel=\"icon\" href=\"/favicon.ico\">";

        var candidates = FaviconParser.Candidates(html, s_page);

        Assert.Equal(candidates.Count, candidates.Distinct().Count());
        Assert.True(candidates.Count <= 6);
    }
}
