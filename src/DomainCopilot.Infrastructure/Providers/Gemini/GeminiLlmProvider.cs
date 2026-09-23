using System.Net.Http.Json;
using System.Text.Json;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;
using Microsoft.Extensions.Options;

namespace DomainCopilot.Infrastructure.Providers.Gemini;

public sealed class GeminiLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmProviderOptions _options;
    private readonly IUsageTracker _usageTracker;

    public GeminiLlmProvider(
        HttpClient httpClient,
        IOptions<LlmProviderOptions> options,
        IUsageTracker usageTracker)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _usageTracker = usageTracker;

        _httpClient.BaseAddress = new Uri(
            "https://generativelanguage.googleapis.com/");
    }

    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(
            "Gemini__ApiKey");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured.");
        }

        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = prompt
                        }
                    }
                }
            }
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/gemini-3.5-flash-lite:generateContent");

        httpRequest.Headers.Add(
            "x-goog-api-key",
            apiKey);

        httpRequest.Content = JsonContent.Create(request);

        using var response = await _httpClient.SendAsync(
            httpRequest,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini API failed with status {(int)response.StatusCode}: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);

        var text = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "Gemini returned an empty response.");
        }

        _usageTracker.Record(
            new LlmUsage(
                Provider: "Gemini",
                Model: _options.Gemini.Model,
                InputTokens: EstimateTokens(prompt),
                OutputTokens: EstimateTokens(text),
                EstimatedCostUsd: 0m));

        return text;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var result = await CompleteAsync(
            prompt,
            cancellationToken);

        yield return result;
    }

    public Task<string> CallWithToolsAsync(
        string prompt,
        IReadOnlyList<string> tools,
        CancellationToken cancellationToken = default)
    {
        return CompleteAsync(prompt, cancellationToken);
    }

    public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<float>();
        }

        var apiKey = Environment.GetEnvironmentVariable("Gemini__ApiKey");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured.");
        }

        var model = _options.Gemini.EmbeddingModel;

        var request = new
        {
            model = $"models/{model}",
            content = new
            {
                parts = new[]
                {
                new
                {
                    text
                }
            }
            }
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{model}:embedContent");

        httpRequest.Headers.Add("x-goog-api-key", apiKey);

        httpRequest.Content = JsonContent.Create(request);

        using var response = await _httpClient.SendAsync(
            httpRequest,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini embedding request failed: " +
                $"{(int)response.StatusCode} {response.ReasonPhrase}. " +
                $"{responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);

        var values = document.RootElement
            .GetProperty("embedding")
            .GetProperty("values");

        var embedding = values
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();

        return embedding;
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(
            1,
            text.Split(
                new[] { ' ', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries).Length);
    }
}