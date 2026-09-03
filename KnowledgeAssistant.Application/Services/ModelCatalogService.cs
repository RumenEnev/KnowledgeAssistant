using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain;

namespace KnowledgeAssistant.Application.Services
{
    public class ModelCatalogService
    {
        private readonly INamedModelCatalogGateway _gateway;

        public ModelCatalogService(INamedModelCatalogGateway gateway)
        {
            _gateway = gateway;
        }

        public Task<IReadOnlyCollection<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken)
        {
            return _gateway.GetModelsAsync(cancellationToken);
        }

        public async Task<int> GetModelContextWindowAsync(string modelName, CancellationToken cancellationToken)
        {
            return (await _gateway.GetModelsAsync(cancellationToken)).FirstOrDefault(m => m.Name == modelName)?.ContextLength ?? 0;
        }
    }
}