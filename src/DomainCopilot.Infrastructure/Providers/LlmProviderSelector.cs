using System.Runtime.CompilerServices;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;
using DomainCopilot.Infrastructure.Providers.Gemini;
using DomainCopilot.Infrastructure.Providers.Local;
using DomainCopilot.Infrastructure.Providers.OpenAI;
using Microsoft.Extensions.Options;
using DomainCopilot.Infrastructure.Providers.OpenRouter;
namespace DomainCopilot.Infrastructure.Providers;

public sealed class LlmProviderSelector : ILlmProvider
{
    private readonly OpenAiLlmProvider _openAiProvider;
    private readonly LocalLlmProvider _localProvider;
    private readonly GeminiLlmProvider _geminiProvider;
    private readonly OpenRouterLlmProvider _openRouterProvider;
    private readonly LlmProviderOptions _options;
    //private readonly OpenAiLlmProvider _openAiProvider;
    public LlmProviderSelector(
        OpenAiLlmProvider openAiProvider,
        LocalLlmProvider localProvider,
        GeminiLlmProvider geminiProvider,
        OpenRouterLlmProvider openRouterProvider,
        IOptions<LlmProviderOptions> options)
    {
        _openAiProvider = openAiProvider;
        _localProvider = localProvider;
        _geminiProvider = geminiProvider;
        _openRouterProvider = openRouterProvider;
        _options = options.Value;
    }

    private ILlmProvider GetProvider(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openai" => _openAiProvider,
            "gemini" => _geminiProvider,
            "local" => _localProvider,
            "openrouter" => _openRouterProvider,
            _ => throw new InvalidOperationException(
                $"Unknown LLM provider: {providerName}")
        };
    }

    private ILlmProvider Primary => GetProvider(_options.PrimaryProvider);

    private ILlmProvider Fallback => GetProvider(_options.FallbackProvider);

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
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var primaryEnumerator = Primary
            .StreamAsync(prompt, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        bool yieldedAnyChunk = false;
        Exception? primaryException = null;

        try
        {
            while (true)
            {
                bool hasNext;

                try
                {
                    hasNext = await primaryEnumerator.MoveNextAsync();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (yieldedAnyChunk)
                    {
                        throw;
                    }

                    primaryException = ex;
                    break;
                }

                if (!hasNext)
                {
                    break;
                }

                yieldedAnyChunk = true;
                yield return primaryEnumerator.Current;
            }
        }
        finally
        {
            await primaryEnumerator.DisposeAsync();
        }

        if (!yieldedAnyChunk && primaryException is not null)
        {
            await foreach (var chunk in Fallback.StreamAsync(
                prompt,
                cancellationToken))
            {
                yield return chunk;
            }
        }
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

    public async Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(
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