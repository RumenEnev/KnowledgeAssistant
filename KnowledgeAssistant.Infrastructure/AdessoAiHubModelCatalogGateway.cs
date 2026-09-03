using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Domain;
using KnowledgeAssistant.Infrastructure.Dto.AiHub;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace KnowledgeAssistant.Infrastructure;

public sealed class AdessoAiHubModelCatalogGateway : INamedModelCatalogGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public AdessoAiHubModelCatalogGateway(HttpClient httpClient, IOptions<AdessoAiHubOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException($"Configuration value '{AdessoAiHubOptions.SectionName}:ApiKey' is required.");
        }
    }

    public string Provider => ModelProviderNames.AdessoAiHub;

    public async Task<IReadOnlyCollection<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"AI Hub model catalogue request failed with status " + $"{(int)response.StatusCode} ({response.StatusCode}): {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<AiHubModelsResponseDto>(stream, JsonOptions, cancellationToken);
        return (result?.Data ?? [])
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .OrderBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .Select(model => new ModelInfo
            {
                Name = model.Id,
                // OpenAI's model endpoint exposes owner, but not Ollama's size,
                // context length, quantization level or parameter count.
                Family = model.OwnedBy
            })
            .ToArray();
    }
}