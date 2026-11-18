using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class MarkdownBlocksTests
{
    [Fact]
    public void Paragrafos_SeparadosPorLinhaEmBranco()
    {
        var blocks = MarkdownBlocks.Parse("Primeira linha\nsegunda linha\n\nOutro parágrafo");

        Assert.Equal(2, blocks.Count);
        Assert.Equal("Primeira linha\nsegunda linha", blocks[0].Text);
        Assert.Equal("Outro parágrafo", blocks[1].Text);
    }

    [Fact]
    public void Listas_ComMarcadorENumero()
    {
        var blocks = MarkdownBlocks.Parse("- um\n* dois\n3. três");

        Assert.Equal([MarkdownBlockKind.Bullet, MarkdownBlockKind.Bullet, MarkdownBlockKind.Numbered], blocks.Select(b => b.Kind));
        Assert.Equal("•", blocks[0].Marker);
        Assert.Equal("3.", blocks[2].Marker);
        Assert.Equal("três", blocks[2].Text);
    }

    [Fact]
    public void Titulo_FicaEmNegrito()
    {
        var block = Assert.Single(MarkdownBlocks.Parse("## Passos"));

        Assert.Equal(MarkdownBlockKind.Heading, block.Kind);
        Assert.All(block.Runs, r => Assert.True(r.Style.HasFlag(InlineStyle.Bold)));
    }

    [Fact]
    public void BlocoDeCodigo_PreservaOConteudo()
    {
        var blocks = MarkdownBlocks.Parse("Veja:\n```csharp\nvar x = 1;\n  **não é negrito**\n```\nFim");

        Assert.Equal([MarkdownBlockKind.Paragraph, MarkdownBlockKind.Code, MarkdownBlockKind.Paragraph], blocks.Select(b => b.Kind));
        Assert.Equal("var x = 1;\n  **não é negrito**", blocks[1].Text);
    }

    [Fact]
    public void BlocoDeCodigoAindaChegando_AparecesMesmoSemFechar()
    {
        var blocks = MarkdownBlocks.Parse("```\nlinha 1\nlinha 2");

        var code = Assert.Single(blocks);
        Assert.Equal(MarkdownBlockKind.Code, code.Kind);
        Assert.Equal("linha 1\nlinha 2", code.Text);
    }

    [Fact]
    public void Inline_NegritoItalicoECodigo()
    {
        var runs = MarkdownBlocks.Inline("a **b** *c* `d` e");

        Assert.Equal(
            [
                new InlineRun("a ", InlineStyle.None),
                new InlineRun("b", InlineStyle.Bold),
                new InlineRun(" ", InlineStyle.None),
                new InlineRun("c", InlineStyle.Italic),
                new InlineRun(" ", InlineStyle.None),
                new InlineRun("d", InlineStyle.Code),
                new InlineRun(" e", InlineStyle.None),
            ],
            runs);
    }

    [Theory]
    [InlineData("2 * 3 = 6")]
    [InlineData("custa **muito")]
    [InlineData("um `crase solto")]
    [InlineData("*")]
    public void MarcadoresSemPar_FicamComoTexto(string text)
    {
        var runs = MarkdownBlocks.Inline(text);

        Assert.Equal(text, string.Concat(runs.Select(r => r.Text)));
        Assert.All(runs, r => Assert.Equal(InlineStyle.None, r.Style));
    }

    [Fact]
    public void NegritoAindaChegando_FicaAbertoAteOFim()
    {
        var runs = MarkdownBlocks.Inline("**texto em negr");

        Assert.Equal("**texto em negr", string.Concat(runs.Select(r => r.Text)));
    }
}
