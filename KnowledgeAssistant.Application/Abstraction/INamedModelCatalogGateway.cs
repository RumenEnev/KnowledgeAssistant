using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Domain;

namespace KnowledgeAssistant.Application.Abstraction;

public interface INamedModelCatalogGateway
{
    public string Provider => ModelProviderNames.Unknown;

    Task<IReadOnlyCollection<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken);
}