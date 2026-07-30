using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain;
using KnowledgeAssistant.Infrastructure.Dto;
using System.Net.Http.Json;
using System.Text.Json;

namespace KnowledgeAssistant.Infrastructure
{
    public class OllamaModelCatalogGateway : IModelCatalogGateway
    {
        private readonly HttpClient _httpClient;

        public OllamaModelCatalogGateway(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyCollection<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetFromJsonAsync<OllamaTagsResponseDto>("/api/tags", cancellationToken);
            if (response is null)
            {
                return Array.Empty<ModelInfo>();
            }

            var models = new List<ModelInfo>();
            foreach (var tag in response.Models)
            {
                var details = await GetModelDetailsAsync(tag.Name, cancellationToken);
                models.Add(new ModelInfo
                {
                    Name = tag.Name,
                    Size = tag.Size,
                    ContextLength = details.ContextLength,
                    Family = details.Family,
                    QuantizationLevel = details.QuantizationLevel,
                    ParameterSize = details.ParameterSize
                });
            }

            return models;
        }

        private async Task<(int? ContextLength, string? Family, string? QuantizationLevel, string? ParameterSize)> GetModelDetailsAsync(
        string modelName, CancellationToken cancellationToken)
        {
            var requestBody = new { model = modelName };

            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/show", requestBody, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (null, null, null, null);
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            int? contextLength = null;
            string? family = null;
            string? quantizationLevel = null;
            string? parameterSize = null;

            if (doc.RootElement.TryGetProperty("model_info", out JsonElement modelInfo))
            {
                foreach (JsonProperty prop in modelInfo.EnumerateObject())
                {
                    if (prop.Name.EndsWith(".context_length", StringComparison.OrdinalIgnoreCase))
                    {
                        contextLength = prop.Value.GetInt32();
                        break;
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("details", out JsonElement details))
            {
                if (details.TryGetProperty("family", out JsonElement familyEl))
                {
                    family = familyEl.GetString();
                }

                if (details.TryGetProperty("quantization_level", out JsonElement quantEl))
                {
                    quantizationLevel = quantEl.GetString();
                }

                if (details.TryGetProperty("parameter_size", out JsonElement paramEl))
                {
                    parameterSize = paramEl.GetString();
                }
            }

            return (contextLength, family, quantizationLevel, parameterSize);
        }
    }
}