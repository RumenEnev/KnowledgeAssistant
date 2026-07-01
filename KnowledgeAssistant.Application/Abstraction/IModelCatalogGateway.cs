using KnowledgeAssistant.Domain;

namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IModelCatalogGateway
    {
        Task<IReadOnlyCollection<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken);
    }
}