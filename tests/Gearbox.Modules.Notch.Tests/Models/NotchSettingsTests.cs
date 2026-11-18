using System.Text.Json;
using System.Text.Json.Serialization;
using Gearbox.Modules.Notch.Models;

namespace Gearbox.Modules.Notch.Tests.Models;

public sealed class NotchSettingsTests
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    [Theory]
    [InlineData("""{ "acrylicBackground": true, "backgroundOpacity": 60 }""", BackgroundStyle.Glass)]
    [InlineData("""{ "acrylicBackground": false, "backgroundOpacity": 100 }""", BackgroundStyle.Opaque)]
    [InlineData("""{ "acrylicBackground": false, "backgroundOpacity": 25 }""", BackgroundStyle.Clear)]
    [InlineData("""{ "backgroundOpacity": 60 }""", BackgroundStyle.Glass)]
    public void ConfiguracaoDaVersaoAnterior_ViraOEstiloMaisProximo(string json, BackgroundStyle expected)
    {
        var settings = JsonSerializer.Deserialize<NotchSettings>(json, s_options)!;

        Assert.Equal(expected, settings.BackgroundStyle);
    }

    [Fact]
    public void EstiloNovoGravado_GanhaDosCamposAntigos()
    {
        var json = """{ "acrylicBackground": true, "backgroundOpacity": 20, "backgroundStyle": "opaque" }""";

        var settings = JsonSerializer.Deserialize<NotchSettings>(json, s_options)!;

        Assert.Equal(BackgroundStyle.Opaque, settings.BackgroundStyle);
    }

    [Fact]
    public void AbaIA_ComecaDesligadaESobreviveAGravacao()
    {
        Assert.False(JsonSerializer.Deserialize<NotchSettings>("{}", s_options)!.ShowAiUsage);

        var json = JsonSerializer.Serialize(new NotchSettings { ShowAiUsage = true }, s_options);

        Assert.True(JsonSerializer.Deserialize<NotchSettings>(json, s_options)!.ShowAiUsage);
    }

    [Fact]
    public void AbaChat_ComecaDesligadaESobreviveAGravacao()
    {
        Assert.False(JsonSerializer.Deserialize<NotchSettings>("{}", s_options)!.ShowChat);

        var json = JsonSerializer.Serialize(new NotchSettings { ShowChat = true }, s_options);

        Assert.True(JsonSerializer.Deserialize<NotchSettings>(json, s_options)!.ShowChat);
    }

    [Fact]
    public void Gravacao_NaoEscreveOsCamposAntigosEPreservaOsNovos()
    {
        var original = new NotchSettings
        {
            BackgroundStyle = BackgroundStyle.Subtle,
            BackgroundColor = "#2A1840",
            UseAccentColor = true,
            Roundness = NotchRoundness.More,
            SpringIntensity = SpringIntensity.Bouncy,
        };

        var json = JsonSerializer.Serialize(original, s_options);
        var copy = JsonSerializer.Deserialize<NotchSettings>(json, s_options)!;

        Assert.DoesNotContain("acrylicBackground", json, StringComparison.Ordinal);
        Assert.DoesNotContain("backgroundOpacity", json, StringComparison.Ordinal);
        Assert.Contains("\"subtle\"", json, StringComparison.Ordinal);
        Assert.Equal(BackgroundStyle.Subtle, copy.BackgroundStyle);
        Assert.Equal("#2A1840", copy.BackgroundColor);
        Assert.True(copy.UseAccentColor);
        Assert.Equal(NotchRoundness.More, copy.Roundness);
        Assert.Equal(SpringIntensity.Bouncy, copy.SpringIntensity);
    }
}
