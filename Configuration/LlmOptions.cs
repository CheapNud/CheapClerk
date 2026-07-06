namespace CheapClerk.Configuration;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public LlmProvider Provider { get; set; } = LlmProvider.Anthropic;

    public AnthropicProviderOptions Anthropic { get; set; } = new();
    public OllamaProviderOptions Ollama { get; set; } = new();
}

public enum LlmProvider
{
    Anthropic,
    Ollama
}

public sealed class AnthropicProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-haiku-4-5-20251001";
}

public sealed class OllamaProviderOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
}
