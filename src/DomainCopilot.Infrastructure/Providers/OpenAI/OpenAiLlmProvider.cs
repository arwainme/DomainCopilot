using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DomainCopilot.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace DomainCopilot.Infrastructure.Providers.OpenAI;

public sealed class OpenAiLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmProviderOptions _options;

    public OpenAiLlmProvider(
        HttpClient httpClient,
        IOptions<LlmProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress = new Uri(
            _options.OpenAI.BaseUrl);

        if (!string.IsNullOrWhiteSpace(
                _options.OpenAI.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _options.OpenAI.ApiKey);
        }
    }

    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.OpenAI.Model,
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

        return result
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
    CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _options.OpenAI.Model,
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

            var line = await reader.ReadLineAsync(
                cancellationToken);

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data:"))
                continue;

            var data = line["data:".Length..].Trim();

            if (data == "[DONE]")
                yield break;

            using var json = JsonDocument.Parse(data);

            var content = json.RootElement
                .GetProperty("choices")[0]
                .GetProperty("delta")
                .GetProperty("content");

            if (content.ValueKind == JsonValueKind.String)
            {
                var text = content.GetString();

                if (!string.IsNullOrEmpty(text))
                    yield return text;
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
            model = _options.OpenAI.EmbeddingModel,
            input = text
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "embeddings",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken);

        var embedding = result
            .GetProperty("data")[0]
            .GetProperty("embedding");

        return embedding
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();
    }
}