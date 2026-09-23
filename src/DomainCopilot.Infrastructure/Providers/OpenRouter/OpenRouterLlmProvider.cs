using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;
using DomainCopilot.Infrastructure.Providers.Gemini;
using Microsoft.Extensions.Options;

namespace DomainCopilot.Infrastructure.Providers.OpenRouter;

public sealed class OpenRouterLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmProviderOptions _options;
    private readonly IUsageTracker _usageTracker;
    private readonly GeminiLlmProvider _geminiProvider;

    public OpenRouterLlmProvider(
        HttpClient httpClient,
        IOptions<LlmProviderOptions> options,
        IUsageTracker usageTracker,
        GeminiLlmProvider geminiProvider)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _usageTracker = usageTracker;
        _geminiProvider = geminiProvider;

        _httpClient.BaseAddress =
            new Uri(_options.OpenRouter.BaseUrl.TrimEnd('/') + "/");

        if (!string.IsNullOrWhiteSpace(_options.OpenRouter.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _options.OpenRouter.ApiKey);
        }
    }

    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.OpenRouter.Model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "chat/completions",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken);

        var inputTokens = 0;
        var outputTokens = 0;

        if (result.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var p))
                inputTokens = p.GetInt32();

            if (usage.TryGetProperty("completion_tokens", out var c))
                outputTokens = c.GetInt32();
        }

        _usageTracker.Record(
            new LlmUsage(
                Provider: "OpenRouter",
                Model: _options.OpenRouter.Model,
                InputTokens: inputTokens,
                OutputTokens: outputTokens,
                EstimatedCostUsd: 0m));

        return result
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.OpenRouter.Model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            stream = true
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "chat/completions")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line =
                await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data:"))
                continue;

            var data =
                line["data:".Length..].Trim();

            if (data == "[DONE]")
                yield break;

            using var json =
                JsonDocument.Parse(data);

            var choices =
                json.RootElement.GetProperty("choices");

            if (choices.GetArrayLength() == 0)
                continue;

            var delta =
                choices[0].GetProperty("delta");

            if (!delta.TryGetProperty(
                    "content",
                    out var content))
                continue;

            if (content.ValueKind == JsonValueKind.String)
            {
                var text = content.GetString();

                if (!string.IsNullOrEmpty(text))
                    yield return text;
            }
        }
    }

    public Task<string> CallWithToolsAsync(
        string prompt,
        IReadOnlyList<string> tools,
        CancellationToken cancellationToken = default)
    {
        return CompleteAsync(prompt, cancellationToken);
    }

    // Keep all embeddings in the same Gemini vector space.
    public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return _geminiProvider.GenerateEmbeddingAsync(
            text,
            cancellationToken);
    }

    public Task<IReadOnlyList<IReadOnlyList<float>>>
        GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
    {
        return _geminiProvider.GenerateEmbeddingsAsync(
            texts,
            cancellationToken);
    }
}