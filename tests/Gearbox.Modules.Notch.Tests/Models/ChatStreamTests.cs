using Gearbox.Modules.Notch.Models;

namespace Gearbox.Modules.Notch.Tests.Models;

public sealed class ChatStreamTests
{
    [Fact]
    public void Delta_ExtraiOTextoDoPrimeiroChoice()
    {
        var chunk = ChatStream.ParseLine("""data: {"choices":[{"delta":{"content":"Olá"},"finish_reason":null}]}""");

        Assert.Equal(new ChatStreamEvent("Olá", null, false), chunk);
    }

    [Fact]
    public void Done_EncerraOStream()
    {
        Assert.Equal(new ChatStreamEvent(null, null, true), ChatStream.ParseLine("data: [DONE]"));
    }

    [Theory]
    [InlineData(": OPENROUTER PROCESSING")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("event: message")]
    [InlineData("data: {nao e json")]
    [InlineData("""data: {"choices":[{"delta":{"role":"assistant"}}]}""")]
    [InlineData("""data: {"choices":[]}""")]
    public void LinhasSemTexto_SaoIgnoradas(string? line)
    {
        Assert.Null(ChatStream.ParseLine(line));
    }

    [Fact]
    public void ErroNoMeioDoStream_EncerraComAMensagem()
    {
        var chunk = ChatStream.ParseLine("""data: {"error":{"code":502,"message":"Provider indisponível"}}""");

        Assert.Equal(new ChatStreamEvent(null, "Provider indisponível", true), chunk);
    }

    [Fact]
    public void FinishReasonErro_EncerraSemMensagem()
    {
        var chunk = ChatStream.ParseLine("""data: {"choices":[{"delta":{"content":""},"finish_reason":"error"}]}""");

        Assert.NotNull(chunk);
        Assert.True(chunk.Value.IsDone);
        Assert.Equal(string.Empty, chunk.Value.Error);
    }

    [Theory]
    [InlineData("""{"error":{"message":"No auth credentials found","code":401}}""", "No auth credentials found")]
    [InlineData("""{"error":"limite atingido"}""", "limite atingido")]
    [InlineData("""{"ok":true}""", null)]
    [InlineData("<html>", null)]
    public void MensagemDeErro_DoCorpoDaResposta(string body, string? expected)
    {
        Assert.Equal(expected, ChatStream.ErrorMessage(body));
    }
}
