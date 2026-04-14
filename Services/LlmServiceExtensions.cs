using Anthropic;
using Anthropic.Core;
using CheapClerk.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace CheapClerk.Services;

public static class LlmServiceExtensions
{
    public static IServiceCollection AddConfiguredChatClient(this IServiceCollection services)
    {
        services.AddSingleton<IChatClient>(sp =>
        {
            var llmOptions = sp.GetRequiredService<IOptions<LlmOptions>>().Value;

            return llmOptions.Provider switch
            {
                LlmProvider.Ollama => BuildOllamaClient(llmOptions.Ollama),
                LlmProvider.Anthropic => BuildAnthropicClient(llmOptions.Anthropic),
                _ => new NoOpChatClient()
            };
        });

        return services;
    }

    private static IChatClient BuildAnthropicClient(AnthropicProviderOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
            return new NoOpChatClient();

        var anthropic = new AnthropicClient(new ClientOptions { ApiKey = opts.ApiKey });
        return anthropic.AsIChatClient(opts.Model);
    }

    private static IChatClient BuildOllamaClient(OllamaProviderOptions opts)
    {
        var ollamaClient = new OllamaApiClient(new Uri(opts.BaseUrl), opts.Model);
        return ollamaClient;
    }

    /// <summary>Placeholder client that returns empty responses when no LLM provider is configured.</summary>
    private sealed class NoOpChatClient : IChatClient
    {
        public ChatClientMetadata Metadata { get; } = new("noop");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? chatOptions = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "LLM provider is not configured.")]));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? chatOptions = null,
            CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<ChatResponseUpdate>();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
