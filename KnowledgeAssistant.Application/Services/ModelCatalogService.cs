using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain;

namespace KnowledgeAssistant.Application.Services
{
    public class ModelCatalogService
    {
        private readonly IModelCatalogGateway _gateway;

        public ModelCatalogService(IModelCatalogGateway gateway)
        {
            _gateway = gateway;
        }

        public Task<IReadOnlyCollection<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken)
        {
            return _gateway.GetModelsAsync(cancellationToken);
        }
    }
}