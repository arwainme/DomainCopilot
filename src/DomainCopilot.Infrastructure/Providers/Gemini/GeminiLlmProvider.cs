using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

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
        var apiKey =
            Environment.GetEnvironmentVariable(
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

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"v1beta/models/{_options.Gemini.Model}:generateContent");

        httpRequest.Headers.Add(
            "x-goog-api-key",
            apiKey);

        httpRequest.Content =
            JsonContent.Create(request);

        using var response =
            await _httpClient.SendAsync(
                httpRequest,
                cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini API failed with status " +
                $"{(int)response.StatusCode}: {responseBody}");
        }

        using var document =
            JsonDocument.Parse(responseBody);

        var text =
            document.RootElement
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
        var apiKey =
            Environment.GetEnvironmentVariable(
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

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"v1beta/models/{_options.Gemini.Model}:streamGenerateContent?alt=sse");

        httpRequest.Headers.Add(
            "x-goog-api-key",
            apiKey);

        httpRequest.Content =
            JsonContent.Create(request);

        using var response =
            await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new HttpRequestException(
                $"Gemini streaming API failed with status " +
                $"{(int)response.StatusCode}: {responseBody}");
        }

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var reader =
            new StreamReader(stream);

        var fullResponse = new StringBuilder();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line =
                await reader.ReadLineAsync(
                    cancellationToken);

            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data =
                line["data:".Length..].Trim();

            if (data == "[DONE]")
            {
                break;
            }

            using var document =
                JsonDocument.Parse(data);

            if (!document.RootElement.TryGetProperty(
                    "candidates",
                    out var candidates) ||
                candidates.GetArrayLength() == 0)
            {
                continue;
            }

            var candidate =
                candidates[0];

            if (!candidate.TryGetProperty(
                    "content",
                    out var content))
            {
                continue;
            }

            if (!content.TryGetProperty(
                    "parts",
                    out var parts) ||
                parts.GetArrayLength() == 0)
            {
                continue;
            }

            var textPart =
                parts[0];

            if (!textPart.TryGetProperty(
                    "text",
                    out var textElement))
            {
                continue;
            }

            var text =
                textElement.GetString();

            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            fullResponse.Append(text);

            yield return text;
        }

        if (fullResponse.Length > 0)
        {
            _usageTracker.Record(
                new LlmUsage(
                    Provider: "Gemini",
                    Model: _options.Gemini.Model,
                    InputTokens: EstimateTokens(prompt),
                    OutputTokens: EstimateTokens(fullResponse.ToString()),
                    EstimatedCostUsd: 0m));
        }
    }

    public Task<string> CallWithToolsAsync(
        string prompt,
        IReadOnlyList<string> tools,
        CancellationToken cancellationToken = default)
    {
        return CompleteAsync(
            prompt,
            cancellationToken);
    }

    public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<float>();
        }

        var apiKey =
            Environment.GetEnvironmentVariable(
                "Gemini__ApiKey");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured.");
        }

        var model =
            _options.Gemini.EmbeddingModel;

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

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"v1beta/models/{model}:embedContent");

        httpRequest.Headers.Add(
            "x-goog-api-key",
            apiKey);

        httpRequest.Content =
            JsonContent.Create(request);

        using var response =
            await _httpClient.SendAsync(
                httpRequest,
                cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini embedding request failed: " +
                $"{(int)response.StatusCode} " +
                $"{response.ReasonPhrase}. " +
                $"{responseBody}");
        }

        using var document =
            JsonDocument.Parse(responseBody);

        var values =
            document.RootElement
                .GetProperty("embedding")
                .GetProperty("values");

        return values
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();
    }

    public async Task<IReadOnlyList<IReadOnlyList<float>>>
        GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
    {
        if (texts is null)
        {
            throw new ArgumentNullException(
                nameof(texts));
        }

        if (texts.Count == 0)
        {
            return Array.Empty<IReadOnlyList<float>>();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var apiKey =
            Environment.GetEnvironmentVariable(
                "Gemini__ApiKey");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured.");
        }

        var model =
            _options.Gemini.EmbeddingModel;

        var requests =
            texts.Select(text => new
            {
                model = $"models/{model}",
                content = new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = text ?? string.Empty
                        }
                    }
                }
            }).ToArray();

        var requestBody = new
        {
            requests
        };

        using var httpRequest =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"v1beta/models/{model}:batchEmbedContents");

        httpRequest.Headers.Add(
            "x-goog-api-key",
            apiKey);

        httpRequest.Content =
            JsonContent.Create(requestBody);

        using var response =
            await _httpClient.SendAsync(
                httpRequest,
                cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini batch embedding request failed: " +
                $"{(int)response.StatusCode} " +
                $"{response.ReasonPhrase}. " +
                $"{responseBody}");
        }

        using var document =
            JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty(
                "embeddings",
                out var embeddingsElement))
        {
            throw new InvalidOperationException(
                "Gemini batch embedding response " +
                "does not contain 'embeddings'. " +
                $"Response: {responseBody}");
        }

        var embeddings =
            new List<IReadOnlyList<float>>();

        foreach (var embeddingElement
                 in embeddingsElement.EnumerateArray())
        {
            if (!embeddingElement.TryGetProperty(
                    "values",
                    out var valuesElement))
            {
                throw new InvalidOperationException(
                    "Gemini returned an embedding " +
                    "without 'values'.");
            }

            var values =
                valuesElement
                    .EnumerateArray()
                    .Select(x => x.GetSingle())
                    .ToArray();

            embeddings.Add(values);
        }

        if (embeddings.Count != texts.Count)
        {
            throw new InvalidOperationException(
                $"Gemini returned {embeddings.Count} embeddings " +
                $"for {texts.Count} input texts.");
        }

        var totalInputTokens =
            texts.Sum(EstimateTokens);

        _usageTracker.Record(
            new LlmUsage(
                Provider: "Gemini",
                Model: model,
                InputTokens: totalInputTokens,
                OutputTokens: 0,
                EstimatedCostUsd: 0m));

        return embeddings;
    }

    private static int EstimateTokens(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(
            1,
            text.Split(
                new[]
                {
                    ' ',
                    '\n',
                    '\r',
                    '\t'
                },
                StringSplitOptions.RemoveEmptyEntries)
                .Length);
    }
}
