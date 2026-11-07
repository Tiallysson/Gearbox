namespace Gearbox.Modules.Notch.Logic;

public enum ClipboardItemKind
{
    Text,
    Image,
    Files,
}

public sealed record ClipboardItem(long Id, ClipboardItemKind Kind, string? Text, string? ImageHash, DateTimeOffset CopiedAt)
{
    public bool HasSameContent(ClipboardItem other) =>
        Kind == other.Kind && Kind switch
        {
            ClipboardItemKind.Image => string.Equals(ImageHash, other.ImageHash, StringComparison.Ordinal),
            _ => string.Equals(Text, other.Text, StringComparison.Ordinal),
        };
}

public sealed class ClipboardHistory
{
    public const int Capacity = 20;

    private readonly List<ClipboardItem> _items = [];

    public IReadOnlyList<ClipboardItem> Items => _items;

    public IReadOnlyList<ClipboardItem> Add(ClipboardItem item)
    {
        var removed = new List<ClipboardItem>();
        var duplicate = _items.FindIndex(existing => existing.HasSameContent(item));
        if (duplicate >= 0)
        {
            removed.Add(_items[duplicate]);
            _items.RemoveAt(duplicate);
        }

        _items.Insert(0, item);
        while (_items.Count > Capacity)
        {
            removed.Add(_items[^1]);
            _items.RemoveAt(_items.Count - 1);
        }

        return removed;
    }

    public ClipboardItem? Find(long id) => _items.Find(item => item.Id == id);

    public ClipboardItem? Remove(long id)
    {
        var item = Find(id);
        if (item is not null)
        {
            _items.Remove(item);
        }

        return item;
    }

    public IReadOnlyList<ClipboardItem> Clear()
    {
        var removed = _items.ToList();
        _items.Clear();
        return removed;
    }
}
