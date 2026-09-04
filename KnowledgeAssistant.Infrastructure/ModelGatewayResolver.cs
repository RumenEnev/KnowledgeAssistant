using KnowledgeAssistant.Application.Abstraction;

namespace KnowledgeAssistant.Infrastructure;

public sealed class ModelGatewayResolver : IModelGatewayResolver
{
    private readonly IReadOnlyDictionary<string, INamedModelGateway> _gateways;

    public ModelGatewayResolver(IEnumerable<INamedModelGateway> gateways)
    {
        ArgumentNullException.ThrowIfNull(gateways);
        var gatewayList = gateways.ToList();
        var duplicate = gatewayList.GroupBy(gateway => gateway.Provider, StringComparer.OrdinalIgnoreCase)
                                    .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"More than one generation gateway is registered " + $"for provider '{duplicate.Key}'.");
        }

        _gateways = gatewayList.ToDictionary(gateway => gateway.Provider, StringComparer.OrdinalIgnoreCase);
        Providers = _gateways.Keys
                            .OrderBy(provider => provider, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
    }

    public IReadOnlyCollection<string> Providers { get; }

    public IModelGateway GetRequiredGateway(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException("The conversation does not have a selected model provider.");
        }

        if (_gateways.TryGetValue(provider, out var gateway))
        {
            return gateway;
        }

        throw new InvalidOperationException($"No generation gateway is registered for provider " + $"'{provider}'. Registered providers: " + $"{string.Join(", ", Providers)}.");
    }
}