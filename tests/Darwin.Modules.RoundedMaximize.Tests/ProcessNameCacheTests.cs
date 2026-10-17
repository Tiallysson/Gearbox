using Darwin.Modules.RoundedMaximize.Logic;

namespace Darwin.Modules.RoundedMaximize.Tests;

public sealed class ProcessNameCacheTests
{
    [Fact]
    public void ResolveUmaVezPorJanela()
    {
        var calls = 0;
        var cache = new ProcessNameCache(_ => { calls++; return "notepad"; });

        Assert.Equal("notepad", cache.Get(1));
        Assert.Equal("notepad", cache.Get(1));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void GuardaTambemResultadoNulo()
    {
        var calls = 0;
        var cache = new ProcessNameCache(_ => { calls++; return null; });

        Assert.Null(cache.Get(1));
        Assert.Null(cache.Get(1));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Remove_ForcaNovaResolucao()
    {
        var calls = 0;
        var cache = new ProcessNameCache(_ => $"p{++calls}");

        cache.Get(1);
        Assert.True(cache.Remove(1));

        Assert.Equal("p2", cache.Get(1));
    }
}
