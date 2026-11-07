using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class ClipboardPreviewTests
{
    [Theory]
    [InlineData("olá", "olá")]
    [InlineData("  linha 1\r\n\r\n   linha 2\t fim  ", "linha 1 linha 2 fim")]
    public void ForText_ColapsaEspacosEQuebras(string text, string expected)
    {
        Assert.Equal(expected, ClipboardPreview.ForText(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t ")]
    public void ForText_SoEspacos_Ignora(string? text)
    {
        Assert.Null(ClipboardPreview.ForText(text));
    }

    [Fact]
    public void FilePaths_SeparaPorLinhaEIgnoraVazias()
    {
        Assert.Equal([@"C:\a\foto.jpg", @"C:\b"], ClipboardPreview.FilePaths("C:\\a\\foto.jpg\n\nC:\\b"));
        Assert.Empty(ClipboardPreview.FilePaths(null));
    }

    [Theory]
    [InlineData(@"C:\fotos\praia.jpg", "praia.jpg")]
    [InlineData(@"C:\fotos\", "fotos")]
    [InlineData(@"D:\", "D:")]
    public void FileName_UsaONomeDoArquivoOuDaPasta(string path, string expected)
    {
        Assert.Equal(expected, ClipboardPreview.FileName(path));
    }

    [Fact]
    public void ForText_TextoLongo_CortaComReticencias()
    {
        var preview = ClipboardPreview.ForText(new string('a', 500));

        Assert.Equal(ClipboardPreview.MaxLength + 1, preview?.Length);
        Assert.EndsWith("…", preview);
    }
}
