using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;
using DomainCopilot.Infrastructure.Providers.Gemini;
using DomainCopilot.Infrastructure.Providers.Local;
using DomainCopilot.Infrastructure.Providers.OpenAI;
using Microsoft.Extensions.Options;

namespace DomainCopilot.Infrastructure.Providers;

public sealed class LlmProviderSelector : ILlmProvider
{
    private readonly OpenAiLlmProvider _openAiProvider;
    private readonly LocalLlmProvider _localProvider;
    private readonly GeminiLlmProvider _geminiProvider;
    private readonly LlmProviderOptions _options;

    public LlmProviderSelector(
        OpenAiLlmProvider openAiProvider,
        LocalLlmProvider localProvider,
        GeminiLlmProvider geminiProvider,
        IOptions<LlmProviderOptions> options)
    {
        _openAiProvider = openAiProvider;
        _localProvider = localProvider;
        _geminiProvider = geminiProvider;
        _options = options.Value;
    }

    private ILlmProvider GetProvider(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openai" => _openAiProvider,
            "gemini" => _geminiProvider,
            "local" => _localProvider,
            _ => throw new InvalidOperationException(
                $"Unknown LLM provider: {providerName}")
        };
    }

    private ILlmProvider Primary =>
        GetProvider(_options.PrimaryProvider);

    private ILlmProvider Fallback =>
        GetProvider(_options.FallbackProvider);

    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await Primary.CompleteAsync(
                prompt,
                cancellationToken);
        }
        catch
        {
            return await Fallback.CompleteAsync(
                prompt,
                cancellationToken);
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        string result;

        try
        {
            result = await Primary.CompleteAsync(
                prompt,
                cancellationToken);
        }
        catch
        {
            result = await Fallback.CompleteAsync(
                prompt,
                cancellationToken);
        }

        yield return result;
    }

    public async Task<string> CallWithToolsAsync(
        string prompt,
        IReadOnlyList<string> tools,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await Primary.CallWithToolsAsync(
                prompt,
                tools,
                cancellationToken);
        }
        catch
        {
            return await Fallback.CallWithToolsAsync(
                prompt,
                tools,
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await Primary.GenerateEmbeddingAsync(
                text,
                cancellationToken);
        }
        catch
        {
            return await Fallback.GenerateEmbeddingAsync(
                text,
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<IReadOnlyList<float>>>
        GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
    {
        try
        {
            return await Primary.GenerateEmbeddingsAsync(
                texts,
                cancellationToken);
        }
        catch
        {
            return await Fallback.GenerateEmbeddingsAsync(
                texts,
                cancellationToken);
        }
    }
}
