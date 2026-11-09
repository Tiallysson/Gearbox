using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class QuickLinkTests
{
    private static readonly QuickLink s_mail = new("a", QuickLinkKind.Web, "Gmail", "https://mail.google.com/");
    private static readonly QuickLink s_tube = new("b", QuickLinkKind.Web, "YouTube", "https://www.youtube.com/");
    private static readonly QuickLink s_calc = new("c", QuickLinkKind.App, "Calculadora", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");

    [Fact]
    public void Add_ColocaNoFim()
    {
        var links = QuickLinkList.Add([s_mail], s_tube);

        Assert.Equal([s_mail, s_tube], links);
    }

    [Fact]
    public void Add_MesmoDestino_NaoDuplica()
    {
        var again = QuickLink.Create(QuickLinkKind.Web, "Outro nome", "HTTPS://MAIL.GOOGLE.COM/");

        var links = QuickLinkList.Add([s_mail], again);

        Assert.Single(links);
    }

    [Fact]
    public void Add_MesmoTextoDeTipoDiferente_NaoEDuplicado()
    {
        var app = new QuickLink("d", QuickLinkKind.App, "x", s_mail.Target);

        Assert.Equal(2, QuickLinkList.Add([s_mail], app).Count);
    }

    [Fact]
    public void Add_ListaCheia_NaoAdiciona()
    {
        var full = Enumerable.Range(0, QuickLinkList.MaxLinks)
            .Select(i => new QuickLink(i.ToString(), QuickLinkKind.Web, "x", $"https://site{i}.com/"))
            .ToList();

        Assert.Equal(QuickLinkList.MaxLinks, QuickLinkList.Add(full, s_tube).Count);
    }

    [Fact]
    public void Remove_TiraSoOLinkPedido()
    {
        Assert.Equal([s_mail, s_calc], QuickLinkList.Remove([s_mail, s_tube, s_calc], "b"));
        Assert.Equal([s_mail], QuickLinkList.Remove([s_mail], "nao-existe"));
    }

    [Theory]
    [InlineData("c", -1, new[] { "a", "c", "b" })]
    [InlineData("a", 1, new[] { "b", "a", "c" })]
    [InlineData("a", -1, new[] { "a", "b", "c" })]
    [InlineData("c", 5, new[] { "a", "b", "c" })]
    [InlineData("x", 1, new[] { "a", "b", "c" })]
    public void Move_TrocaDePosicaoSemSairDaLista(string id, int offset, string[] expected)
    {
        var moved = QuickLinkList.Move([s_mail, s_tube, s_calc], id, offset);

        Assert.Equal(expected, moved.Select(l => l.Id));
    }

    [Fact]
    public void ForInstalledApp_AppDoWindowsViraApp()
    {
        var link = QuickLink.ForInstalledApp("Calculadora", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");

        Assert.Equal(QuickLinkKind.App, link.Kind);
        Assert.Equal("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", link.Target);
    }

    [Theory]
    [InlineData("{6D809377-6AF0-444B-8957-A3773F02200E}\\Notepad++\\notepad++.exe")]
    [InlineData("MSEdge")]
    public void ForInstalledApp_CaminhoOuIdDeAppNaoViraSite(string parsingName)
    {
        Assert.Equal(QuickLinkKind.App, QuickLink.ForInstalledApp("x", parsingName).Kind);
    }

    [Fact]
    public void ForInstalledApp_AtalhoDeSiteDoMenuIniciarViraSite()
    {
        var link = QuickLink.ForInstalledApp("Discord", "https://www.geeks3d.com/discord/");

        Assert.Equal(QuickLinkKind.Web, link.Kind);
        Assert.Equal("https://www.geeks3d.com/discord/", link.Target);
    }

    [Fact]
    public void Create_GeraIdsDiferentes()
    {
        Assert.NotEqual(QuickLink.Create(QuickLinkKind.Web, "a", "b").Id, QuickLink.Create(QuickLinkKind.Web, "a", "b").Id);
    }

    [Theory]
    [InlineData("https://mail.google.com/", true)]
    [InlineData("http://localhost:3000/", true)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData(@"C:\Windows\System32\cmd.exe", false)]
    [InlineData("ms-settings:privacy", false)]
    [InlineData("", false)]
    public void IsSafe_LinkWeb_SoHttpEHttps(string target, bool expected)
    {
        Assert.Equal(expected, QuickLink.Create(QuickLinkKind.Web, "x", target).IsSafe());
    }

    [Theory]
    [InlineData("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", true)]
    [InlineData(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Notepad++\notepad++.exe", true)]
    [InlineData(@"..\..\Users\x\malware.exe", false)]
    [InlineData("app\" /c calc", false)]
    [InlineData("   ", false)]
    public void IsSafe_LinkDeApp_SemCaminhoRelativoNemAspas(string target, bool expected)
    {
        Assert.Equal(expected, QuickLink.Create(QuickLinkKind.App, "x", target).IsSafe());
    }

    [Theory]
    [InlineData(@"..\..\evil")]
    [InlineData("abc")]
    [InlineData(null)]
    public void IsSafe_IdForaDoFormato_Recusa(string? id)
    {
        Assert.False(new QuickLink(id!, QuickLinkKind.Web, "x", "https://a.com/").IsSafe());
        Assert.False(QuickLink.IsSafeId(id));
    }

    [Fact]
    public void Safe_DescartaLinksAdulteradosENulos()
    {
        var good = QuickLink.Create(QuickLinkKind.Web, "Gmail", "https://mail.google.com/");
        var evil = QuickLink.Create(QuickLinkKind.Web, "Evil", @"C:\Windows\System32\cmd.exe");

        Assert.Equal([good], QuickLinkList.Safe([good, evil, null, s_mail]));
        Assert.Empty(QuickLinkList.Safe(null));
    }
}
