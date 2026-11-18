namespace Gearbox.Modules.Notch.Logic;

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
