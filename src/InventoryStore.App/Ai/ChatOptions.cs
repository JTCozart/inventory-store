namespace InventoryStore.App.Ai;

public class ChatOptions
{
    // NVIDIA's OpenAI-compatible hosted inference endpoint (build.nvidia.com) -- called at
    // {ApiBaseUrl}/chat/completions with the operator's own API key (module.ai.apiKey,
    // configured in Settings > Modules > AI Assistant) as the bearer token.
    public string ApiBaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1";

    // Default text model; an operator can override it per-install via module.ai.model.
    public string TextModel { get; set; } = "nvidia/nemotron-3-ultra-550b-a55b";

    public int MaxOutputTokens { get; set; } = 1536;

    // Low on purpose: this assistant states facts pulled from ChatTools query results,
    // not creative writing -- a high temperature would make it more likely to embellish
    // beyond what a tool actually returned.
    public float Temperature { get; set; } = 0.2f;

    // Upper bound on the tool-call -> tool-result -> tool-call loop in
    // ChatOrchestrationService before it's forced to answer with whatever it has so far.
    public int MaxToolCallIterations { get; set; } = 3;

    // How many prior turns (user+assistant pairs) to include as context for a follow-up question.
    public int MaxHistoryTurns { get; set; } = 10;

    public int RequestTimeoutSeconds { get; set; } = 180;

    public int MaxRetryAttempts { get; set; } = 3;
}
