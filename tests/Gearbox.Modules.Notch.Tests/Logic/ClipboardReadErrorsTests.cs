using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class ClipboardReadErrorsTests
{
    [Theory]
    [InlineData(0x800401D0u)]
    [InlineData(0x80040064u)]
    [InlineData(0x8004006Au)]
    [InlineData(0x80010001u)]
    [InlineData(0x8001010Eu)]
    public void IsTransient_ErrosEsperadosDaAreaDeTransferencia(uint hresult)
    {
        Assert.True(ClipboardReadErrors.IsTransient(unchecked((int)hresult)));
    }

    [Theory]
    [InlineData(0x80004005u)]
    [InlineData(0x80070005u)]
    [InlineData(0x80020001u)]
    [InlineData(0u)]
    public void IsTransient_OutrosErros_NaoSaoTransitorios(uint hresult)
    {
        Assert.False(ClipboardReadErrors.IsTransient(unchecked((int)hresult)));
    }
}
