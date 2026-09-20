using System.Net.Http.Json;
using System.Text.Json;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Infrastructure.Providers;
using Microsoft.Extensions.Options;

namespace DomainCopilot.Infrastructure.Providers.Local;

public sealed class LocalLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmProviderOptions _options;

    public LocalLlmProvider(
        HttpClient httpClient,
        IOptions<LlmProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

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

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken);

        return result
            .GetProperty("response")
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
        return await CompleteAsync(prompt, cancellationToken);
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

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken);

        return result
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();
    }
}
