using Gearbox.Modules.Notch.Logic;

namespace Gearbox.Modules.Notch.Tests.Logic;

public sealed class ChatModelsTests
{
    [Fact]
    public void Lista_SoModelosDeTextoComAutoPrimeiro()
    {
        var json = """
            {
              "data": [
                { "id": "openai/gpt-5", "name": "OpenAI: GPT-5", "architecture": { "output_modalities": ["text"] } },
                { "id": "black-forest-labs/flux", "name": "FLUX", "architecture": { "output_modalities": ["image"] } },
                { "id": "anthropic/claude-opus-5.5", "name": "Anthropic: Claude Opus 5.5" },
                { "id": "openrouter/free", "name": "Free Models Router", "architecture": { "output_modalities": ["text", "image"] } },
                { "id": "openai/gpt-5", "name": "duplicado" },
                { "name": "sem id" }
              ]
            }
            """;

        var models = ChatModels.Parse(json);

        Assert.Equal(["openrouter/free", "anthropic/claude-opus-5.5", "openai/gpt-5"], models.Select(m => m.Id));
        Assert.Equal("Free Models Router", models[0].Name);
    }

    [Fact]
    public void SemData_ListaVazia()
    {
        Assert.Empty(ChatModels.Parse("""{ "error": "x" }"""));
    }

    [Fact]
    public void ModeloEscolhidoForaDaLista_EntraNoTopo()
    {
        var models = new List<ChatModel> { new("openrouter/auto", "Auto Router") };

        var withSelected = ChatModels.WithSelected(models, "meu/modelo");

        Assert.Equal(["meu/modelo", "openrouter/auto"], withSelected.Select(m => m.Id));
        Assert.Same(models, ChatModels.WithSelected(models, "openrouter/auto"));
    }
}
