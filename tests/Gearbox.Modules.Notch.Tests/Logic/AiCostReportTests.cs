using System.Text.Json;
using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class AiCostReportTests
{
    [Fact]
    public void Anthropic_SomaOsCentavosDeTodosOsDiasEmDolares()
    {
        var json = """
            {
              "data": [
                { "starting_at": "2026-11-01T00:00:00Z", "ending_at": "2026-11-02T00:00:00Z",
                  "results": [ { "amount": "123.45", "currency": "USD" }, { "amount": "76.55", "currency": "USD" } ] },
                { "starting_at": "2026-11-02T00:00:00Z", "ending_at": "2026-11-03T00:00:00Z", "results": [] },
                { "starting_at": "2026-11-03T00:00:00Z", "ending_at": "2026-11-04T00:00:00Z",
                  "results": [ { "amount": "1000", "currency": "USD" } ] }
              ],
              "has_more": false,
              "next_page": null
            }
            """;

        var page = AiCostReport.Parse(AiProvider.Anthropic, json);

        Assert.Equal(12.00m, page.Dollars);
        Assert.Null(page.NextPage);
    }

    [Fact]
    public void OpenAi_SomaOsValoresJaEmDolares()
    {
        var json = """
            {
              "object": "page",
              "data": [
                { "object": "bucket", "start_time": 1793491200, "end_time": 1793577600,
                  "results": [ { "object": "organization.costs.result", "amount": { "value": 0.06, "currency": "usd" } } ] },
                { "object": "bucket", "start_time": 1793577600, "end_time": 1793664000,
                  "results": [ { "object": "organization.costs.result", "amount": { "value": 4.5, "currency": "usd" } } ] }
              ],
              "has_more": false,
              "next_page": null
            }
            """;

        var page = AiCostReport.Parse(AiProvider.OpenAi, json);

        Assert.Equal(4.56m, page.Dollars);
    }

    [Theory]
    [InlineData(AiProvider.Anthropic)]
    [InlineData(AiProvider.OpenAi)]
    public void ComMaisPaginas_DevolveOCursorDaProxima(AiProvider provider)
    {
        var json = """{ "data": [], "has_more": true, "next_page": "page_abc" }""";

        var page = AiCostReport.Parse(provider, json);

        Assert.Equal(0m, page.Dollars);
        Assert.Equal("page_abc", page.NextPage);
    }

    [Fact]
    public void CursorSemHasMore_EIgnorado()
    {
        var page = AiCostReport.Parse(AiProvider.Anthropic, """{ "data": [], "has_more": false, "next_page": "page_abc" }""");

        Assert.Null(page.NextPage);
    }

    [Theory]
    [InlineData(AiProvider.Anthropic, """{ "data": [ { "results": [ { "amount": { "value": 1 } } ] } ] }""")]
    [InlineData(AiProvider.OpenAi, """{ "data": [ { "results": [ { "amount": "100" } ] } ] }""")]
    [InlineData(AiProvider.Anthropic, """{ "data": [ { "results": [ { "amount": "abc" } ] } ] }""")]
    [InlineData(AiProvider.OpenAi, """{ "data": [ { "results": [ { } ] } ] }""")]
    public void ValorEmFormatoInesperado_ContaComoZero(AiProvider provider, string json)
    {
        Assert.Equal(0m, AiCostReport.Parse(provider, json).Dollars);
    }

    [Fact]
    public void JsonInvalido_Lanca()
    {
        Assert.ThrowsAny<JsonException>(() => AiCostReport.Parse(AiProvider.OpenAi, "<html>"));
    }
}
