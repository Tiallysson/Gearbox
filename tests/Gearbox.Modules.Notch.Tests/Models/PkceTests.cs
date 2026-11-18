using Gearbox.Modules.Notch.Models;

namespace Gearbox.Modules.Notch.Tests.Models;

public sealed class PkceTests
{
    private static readonly byte[] s_rfc7636Bytes =
    [
        116, 24, 223, 180, 151, 153, 224, 37, 79, 250, 96, 125, 216, 173, 187, 186,
        22, 212, 37, 77, 105, 214, 191, 240, 91, 88, 5, 88, 83, 132, 141, 121,
    ];

    [Fact]
    public void Verificador_ComOsBytesDaRfc7636()
    {
        Assert.Equal("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk", Pkce.CreateVerifier(s_rfc7636Bytes));
    }

    [Fact]
    public void Desafio_S256DaRfc7636()
    {
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", Pkce.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void Base64Url_SemPreenchimentoESemCaracteresDeUrl()
    {
        var encoded = Pkce.Base64Url([0xFB, 0xFF, 0xBF]);

        Assert.Equal("-_-_", encoded);
        Assert.DoesNotContain('=', Pkce.Base64Url([1, 2]));
    }
}
