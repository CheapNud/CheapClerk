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
                _ => throw new InvalidOperationException($"Unknown LLM provider: {llmOptions.Provider}")
            };
        });

        return services;
    }

    private static IChatClient BuildAnthropicClient(AnthropicProviderOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
            throw new InvalidOperationException("Anthropic API key is not configured (Llm:Anthropic:ApiKey).");

        var anthropic = new AnthropicClient(new ClientOptions { ApiKey = opts.ApiKey });
        return anthropic.AsIChatClient(opts.Model);
    }

    private static IChatClient BuildOllamaClient(OllamaProviderOptions opts)
    {
        var ollamaClient = new OllamaApiClient(new Uri(opts.BaseUrl), opts.Model);
        return ollamaClient;
    }
}
