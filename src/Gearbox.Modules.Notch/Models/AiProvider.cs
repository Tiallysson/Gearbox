namespace Gearbox.Modules.Notch.Models;

public enum AiProvider
{
    Anthropic,
    OpenAi,
    OpenRouter,
}

public enum AiUsageStatus
{
    Ok,
    InvalidKey,
    Unavailable,
}
