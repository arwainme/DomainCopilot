using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;
using DomainCopilot.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace DomainCopilot.Infrastructure.Providers.Local;

public sealed class LocalLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmProviderOptions _options;
    private readonly IUsageTracker _usageTracker;

    public LocalLlmProvider(
        HttpClient httpClient,
        IOptions<LlmProviderOptions> options,
        IUsageTracker usageTracker)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _usageTracker = usageTracker;

        _httpClient.BaseAddress = new Uri(
            _options.Local.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.Local.Model,
            prompt,
            stream = false
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/generate",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken);

        var output =
            result
                .GetProperty("response")
                .GetString()
            ?? string.Empty;

        _usageTracker.Record(
            new LlmUsage(
                Provider: "Local",
                Model: _options.Local.Model,
                InputTokens: EstimateTokens(prompt),
                OutputTokens: EstimateTokens(output),
                EstimatedCostUsd: 0m));

        return output;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.Local.Model,
            prompt,
            stream = true
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "api/generate")
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

            var line = await reader.ReadLineAsync(
                cancellationToken);

            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var json = JsonDocument.Parse(line);

            if (json.RootElement.TryGetProperty(
                    "response",
                    out var content))
            {
                var text = content.GetString();

                if (!string.IsNullOrEmpty(text))
                    yield return text;
            }

            if (json.RootElement.TryGetProperty(
                    "done",
                    out var done) &&
                done.GetBoolean())
            {
                yield break;
            }
        }
    }

    public async Task<string> CallWithToolsAsync(
        string prompt,
        IReadOnlyList<string> tools,
        CancellationToken cancellationToken = default)
    {
        return await CompleteAsync(
            prompt,
            cancellationToken);
    }

    public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.Local.EmbeddingModel,
            prompt = text
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/embeddings",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken);

        return result
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();
    }

    public async Task<IReadOnlyList<IReadOnlyList<float>>>
        GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
    {
        var embeddings =
            new List<IReadOnlyList<float>>(texts.Count);

        foreach (var text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var embedding =
                await GenerateEmbeddingAsync(
                    text,
                    cancellationToken);

            embeddings.Add(embedding);
        }

        return embeddings;
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return Math.Max(
            1,
            (int)Math.Ceiling(text.Length / 4.0));
    }
}
