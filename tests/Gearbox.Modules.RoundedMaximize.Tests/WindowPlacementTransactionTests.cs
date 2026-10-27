using Gearbox.Interop;
using Gearbox.Modules.RoundedMaximize.Native;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gearbox.Modules.RoundedMaximize.Tests;

public sealed class WindowPlacementTransactionTests
{
    [Theory]
    [InlineData("SetWindowPlacement")]
    [InlineData("SetWindowPos")]
    [InlineData("GetWindowPlacement")]
    [InlineData("GetWindowRect")]
    public void FalhaAposInicio_RestauraEstadoOriginal(string operation)
    {
        var api = new FakeApi { FailedOperation = operation };
        var transaction = Create(api);

        var result = transaction.Apply(42, 8);

        Assert.Null(result.Window);
        Assert.Equal(operation, result.FailedOperation);
        Assert.Equal(5, result.Error);
        Assert.Equal(FakeApi.Original, api.Current);
        Assert.Equal(FakeApi.Original, api.Writes[^1]);
        Assert.False(transaction.HasPending(42));
    }

    [Fact]
    public void RollbackFalha_MantemOriginalParaNovaTentativa()
    {
        var api = new FakeApi { FailedOperation = "SetWindowPos", FailRestore = true };
        var transaction = Create(api);

        Assert.Null(transaction.Apply(42, 8).Window);
        Assert.True(transaction.HasPending(42));
        Assert.Equal(ShowWindowCommand.Normal, api.Current.ShowCommand);

        api.FailRestore = false;
        transaction.RestorePending();

        Assert.Equal(FakeApi.Original, api.Current);
        Assert.False(transaction.HasPending(42));
    }

    [Fact]
    public void ExcecaoAposMutacao_TambemFazRollback()
    {
        var api = new FakeApi { ThrowOnBounds = true };
        var transaction = Create(api);

        Assert.Throws<InvalidOperationException>(() => transaction.Apply(42, 8));

        Assert.Equal(FakeApi.Original, api.Current);
        Assert.False(transaction.HasPending(42));
    }

    [Fact]
    public void Sucesso_EntregaEstadoParaRastreamentoSemRollback()
    {
        var api = new FakeApi();
        var transaction = Create(api);

        var result = transaction.Apply(42, 8);

        Assert.NotNull(result.Window);
        Assert.Equal(FakeApi.Original, result.Window.OriginalPlacement);
        Assert.Equal(new PixelRect(8, 8, 1912, 1072), result.Window.PseudoWindowRect);
        Assert.Equal(api.Current.NormalPosition, result.Window.PseudoNormalPosition);
        Assert.Single(api.Writes);
        Assert.False(transaction.HasPending(42));
    }

    [Fact]
    public void LeituraInicialFalha_NaoModificaJanela()
    {
        var api = new FakeApi { FailInitialRead = true };
        var transaction = Create(api);

        Assert.Null(transaction.Apply(42, 8).Window);

        Assert.Empty(api.Writes);
        Assert.False(transaction.HasPending(42));
    }

    private static WindowPlacementTransaction Create(FakeApi api) => new(api, NullLogger.Instance);

    private sealed class FakeApi : IWindowPlacementApi
    {
        public static readonly WindowPlacement Original = new(
            ShowWindowCommand.ShowMaximized, new PixelRect(100, 100, 900, 700), 2, -1, -1, -1, -1);

        public WindowPlacement Current { get; private set; } = Original;
        public List<WindowPlacement> Writes { get; } = [];
        public string? FailedOperation { get; init; }
        public bool FailInitialRead { get; init; }
        public bool FailRestore { get; set; }
        public bool ThrowOnBounds { get; init; }
        public int LastError => 5;

        public bool TryGetPlacement(nint hwnd, out WindowPlacement placement)
        {
            placement = Current;
            return !FailInitialRead && !(Writes.Count > 0 && FailedOperation == "GetWindowPlacement");
        }

        public bool TryGetWorkArea(nint hwnd, out PixelRect workArea)
        {
            workArea = new PixelRect(0, 0, 1920, 1080);
            return true;
        }

        public bool TrySetPlacement(nint hwnd, WindowPlacement placement)
        {
            Writes.Add(placement);
            if (placement == Original ? FailRestore : FailedOperation == "SetWindowPlacement")
            {
                return false;
            }

            Current = placement;
            return true;
        }

        public FrameInsets GetFrameInsets(nint hwnd) => FrameInsets.None;

        public bool TrySetBounds(nint hwnd, PixelRect bounds)
        {
            if (ThrowOnBounds)
            {
                throw new InvalidOperationException("Falha simulada");
            }

            if (FailedOperation == "SetWindowPos")
            {
                return false;
            }

            Current = Current with { NormalPosition = bounds };
            return true;
        }

        public bool TryGetWindowRect(nint hwnd, out PixelRect rect)
        {
            rect = Current.NormalPosition;
            return FailedOperation != "GetWindowRect";
        }
    }
}
