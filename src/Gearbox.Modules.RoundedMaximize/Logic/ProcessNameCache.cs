namespace Gearbox.Modules.RoundedMaximize.Logic;

/// <summary>
/// Cache de nome de processo por janela. O processo dono de um hwnd nunca muda, então basta
/// resolver uma vez e remover a entrada quando a janela for destruída.
/// </summary>
public sealed class ProcessNameCache(Func<nint, string?> resolver)
{
    private readonly Dictionary<nint, string?> _names = [];

    public int Count => _names.Count;

    public string? Get(nint hwnd)
    {
        if (!_names.TryGetValue(hwnd, out var name))
        {
            name = resolver(hwnd);
            _names[hwnd] = name;
        }

        return name;
    }

    public bool Remove(nint hwnd) => _names.Remove(hwnd);

    public void Clear() => _names.Clear();
}
