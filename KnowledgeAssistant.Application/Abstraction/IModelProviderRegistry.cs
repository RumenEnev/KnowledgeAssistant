namespace KnowledgeAssistant.Application.Abstraction;

public interface IModelProviderRegistry
{
    IReadOnlyCollection<string> Providers { get; }

    bool TryGetCatalogGateway(string provider, out INamedModelCatalogGateway gateway);
}