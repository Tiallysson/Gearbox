using System.Net;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class NetworkAddressRulesTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700:4700::1111")]
    public void IsPublic_EnderecosDaInternet(string address)
    {
        Assert.True(NetworkAddressRules.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:192.168.0.10")]
    public void IsPublic_RedeInternaELoopback_NaoSao(string address)
    {
        Assert.False(NetworkAddressRules.IsPublic(IPAddress.Parse(address)));
    }

    [Fact]
    public void AllPublic_UmEnderecoInternoBastaParaRecusar()
    {
        Assert.False(NetworkAddressRules.AllPublic([IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.1")]));
        Assert.False(NetworkAddressRules.AllPublic([]));
        Assert.True(NetworkAddressRules.AllPublic([IPAddress.Parse("8.8.8.8")]));
    }
}
