using KnowledgeAssistant.Application.Abstraction;

namespace KnowledgeAssistant.Application.Services;

public sealed class ModelProviderRegistry : IModelProviderRegistry
{
    private readonly IReadOnlyDictionary<string, INamedModelCatalogGateway> _gateways;

    public ModelProviderRegistry(IEnumerable<INamedModelCatalogGateway> gateways)
    {
        ArgumentNullException.ThrowIfNull(gateways);

        var gatewayList = gateways.ToList();
        var duplicate = gatewayList
            .GroupBy(gateway => gateway.Provider, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"More than one model catalog is registered for provider '{duplicate.Key}'.");
        }

        _gateways = gatewayList.ToDictionary(gateway => gateway.Provider, StringComparer.OrdinalIgnoreCase);
        Providers = _gateways.Keys
                    .OrderBy(provider => provider, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
    }

    public IReadOnlyCollection<string> Providers { get; }

    public bool TryGetCatalogGateway(string provider, out INamedModelCatalogGateway gateway)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            gateway = null!;
            return false;
        }

        return _gateways.TryGetValue(provider, out gateway!);
    }
}