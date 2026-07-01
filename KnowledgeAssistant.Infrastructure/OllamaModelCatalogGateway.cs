using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain;
using KnowledgeAssistant.Infrastructure.Dto;
using System.Net.Http.Json;

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

            return response.Models
                .Select(x => new ModelInfo
                {
                    Name = x.Name
                })
                .ToList();
        }
    }
}