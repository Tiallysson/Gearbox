using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class ClipboardHistoryTests
{
    private static readonly DateTimeOffset s_now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Add_MaisRecenteFicaNoTopo()
    {
        var history = new ClipboardHistory();

        history.Add(Text(1, "a"));
        history.Add(Text(2, "b"));

        Assert.Equal([2L, 1L], history.Items.Select(item => item.Id));
    }

    [Fact]
    public void Add_TextoRepetidoSobeSemDuplicar()
    {
        var history = new ClipboardHistory();
        history.Add(Text(1, "a"));
        history.Add(Text(2, "b"));

        var removed = history.Add(Text(3, "a"));

        Assert.Equal([3L, 2L], history.Items.Select(item => item.Id));
        Assert.Equal(1L, Assert.Single(removed).Id);
    }

    [Fact]
    public void Add_ImagemRepetidaPeloHashSobeSemDuplicar()
    {
        var history = new ClipboardHistory();
        history.Add(Image(1, "hash-a"));
        history.Add(Text(2, "hash-a"));

        history.Add(Image(3, "hash-a"));

        Assert.Equal([3L, 2L], history.Items.Select(item => item.Id));
    }

    [Fact]
    public void Add_MesmosArquivosSobemSemDuplicar()
    {
        var history = new ClipboardHistory();
        history.Add(Files(1, "C:\\a.jpg"));
        history.Add(Text(2, "C:\\a.jpg"));

        history.Add(Files(3, "C:\\a.jpg"));

        Assert.Equal([3L, 2L], history.Items.Select(item => item.Id));
    }

    [Fact]
    public void Add_PassandoDoLimiteDevolveOMaisAntigo()
    {
        var history = new ClipboardHistory();
        for (var id = 1; id <= ClipboardHistory.Capacity; id++)
        {
            history.Add(Text(id, $"t{id}"));
        }

        var removed = history.Add(Text(99, "novo"));

        Assert.Equal(ClipboardHistory.Capacity, history.Items.Count);
        Assert.Equal(1L, Assert.Single(removed).Id);
        Assert.Equal(99L, history.Items[0].Id);
    }

    [Fact]
    public void Remove_TiraSoOItemPedido()
    {
        var history = new ClipboardHistory();
        history.Add(Text(1, "a"));
        history.Add(Text(2, "b"));

        Assert.Equal(1L, history.Remove(1)?.Id);
        Assert.Null(history.Remove(1));
        Assert.Equal([2L], history.Items.Select(item => item.Id));
    }

    [Fact]
    public void Clear_EsvaziaEDevolveTudo()
    {
        var history = new ClipboardHistory();
        history.Add(Text(1, "a"));
        history.Add(Image(2, "h"));

        var removed = history.Clear();

        Assert.Empty(history.Items);
        Assert.Equal(2, removed.Count);
    }

    private static ClipboardItem Text(long id, string text) => new(id, ClipboardItemKind.Text, text, null, s_now);

    private static ClipboardItem Files(long id, string paths) => new(id, ClipboardItemKind.Files, paths, null, s_now);

    private static ClipboardItem Image(long id, string hash) => new(id, ClipboardItemKind.Image, null, hash, s_now);
}
