using System.Security.Cryptography;
using System.Text;

namespace Gearbox.Modules.Notch.Logic;

public static class Pkce
{
    public const string Method = "S256";

    public static string CreateVerifier(ReadOnlySpan<byte> randomBytes) => Base64Url(randomBytes);

    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
