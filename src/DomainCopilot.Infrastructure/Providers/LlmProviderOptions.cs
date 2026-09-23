namespace DomainCopilot.Infrastructure.Providers;

public sealed class LlmProviderOptions
{
    public string PrimaryProvider { get; set; } = "OpenRouter";

    public string FallbackProvider { get; set; } = "Gemini";

    public OpenAiOptions OpenAI { get; set; } = new();

    public OpenRouterOptions OpenRouter { get; set; } = new();

    public GeminiLlmOptions Gemini { get; set; } = new();

    public LocalLlmOptions Local { get; set; } = new();
}

public sealed class OpenAiOptions
{
    public string BaseUrl { get; set; } =
        "https://api.openai.com/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-mini";

    public string EmbeddingModel { get; set; } =
        "text-embedding-3-small";
}

public sealed class OpenRouterOptions
{
    public string BaseUrl { get; set; } =
        "https://openrouter.ai/api/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } =
        "openrouter/free";
}

public sealed class GeminiLlmOptions
{
    public string BaseUrl { get; set; } =
        "https://generativelanguage.googleapis.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } =
        "gemini-3.5-flash-lite";

    public string EmbeddingModel { get; set; } =
        "gemini-embedding-2";
}

public sealed class LocalLlmOptions
{
    public string BaseUrl { get; set; } =
        "http://localhost:11434";

    public string Model { get; set; } = "llama3.2";

    public string EmbeddingModel { get; set; } =
        "nomic-embed-text";
}