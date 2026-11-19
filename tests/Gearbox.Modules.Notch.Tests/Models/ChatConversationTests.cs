using Gearbox.Modules.Notch.Models;

namespace Gearbox.Modules.Notch.Tests.Models;

public sealed class ChatConversationTests
{
    [Fact]
    public void NovoId_EValidoParaNomeDeArquivo()
    {
        Assert.True(ChatConversation.IsValidId(ChatConversation.NewId()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../../settings/Notch")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcde")]
    public void IdsInvalidos_SaoRecusados(string? id)
    {
        Assert.False(ChatConversation.IsValidId(id));
    }

    [Fact]
    public void Titulo_JuntaEspacosECorta()
    {
        Assert.Equal("Me explica PKCE", ChatConversation.TitleFrom("  Me   explica\n PKCE  "));

        var longo = ChatConversation.TitleFrom(new string('a', 60));
        Assert.Equal(40, longo.Length);
        Assert.EndsWith("…", longo, StringComparison.Ordinal);
    }

    [Fact]
    public void Contexto_MantemAsMensagensMaisRecentesDentroDoLimite()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, new string('a', 50)),
            new(ChatRole.Assistant, new string('b', 50)),
            new(ChatRole.User, new string('c', 50)),
            new(ChatRole.Assistant, new string('d', 50)),
            new(ChatRole.User, "e"),
        };

        var window = ChatContext.Window(messages, characterBudget: 120);

        Assert.Equal(["c", "d", "e"], window.Select(m => m.Content[..1]));
    }

    [Fact]
    public void Contexto_NaoComecaComResposta()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, new string('a', 100)),
            new(ChatRole.Assistant, "b"),
            new(ChatRole.User, "c"),
        };

        var window = ChatContext.Window(messages, characterBudget: 10);

        Assert.Equal(ChatRole.User, window[0].Role);
        Assert.Equal("c", window[^1].Content);
    }

    [Fact]
    public void Contexto_SempreLevaAUltimaMensagemMesmoGrande()
    {
        var window = ChatContext.Window([new ChatMessage(ChatRole.User, new string('x', 500))], characterBudget: 10);

        Assert.Single(window);
    }

    [Fact]
    public void Contexto_PulaMensagensVazias()
    {
        var window = ChatContext.Window(
        [
            new ChatMessage(ChatRole.User, "oi"),
            new ChatMessage(ChatRole.Assistant, string.Empty),
            new ChatMessage(ChatRole.User, "tudo bem?"),
        ]);

        Assert.Equal(["oi", "tudo bem?"], window.Select(m => m.Content));
    }
}
