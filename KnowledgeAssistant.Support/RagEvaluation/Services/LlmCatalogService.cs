using KnowledgeAssistant.Application.Abstraction;
using Microsoft.Extensions.Configuration;

namespace RagEvaluation.Desktop.Services;

public sealed class LlmCatalogService
{
    private readonly IModelProviderRegistry _providerRegistry;
    private readonly IModelGatewayResolver _gatewayResolver;
    private readonly IConfiguration _configuration;

    public LlmCatalogService(
        IModelProviderRegistry providerRegistry,
        IModelGatewayResolver gatewayResolver,
        IConfiguration configuration)
    {
        _providerRegistry = providerRegistry;
        _gatewayResolver = gatewayResolver;
        _configuration = configuration;
    }

    public IReadOnlyList<string> Providers => _providerRegistry.Providers.ToList();

    public string? DefaultChatModel => _configuration["Llm:ChatModel"];

    public string? DefaultJudgeModel => _configuration["Llm:JudgeModel"];

    public async Task<IReadOnlyList<string>> GetModelsAsync(string provider, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider) || !_providerRegistry.TryGetCatalogGateway(provider, out var catalogGateway))
        {
            return Array.Empty<string>();
        }

        var models = await catalogGateway.GetModelsAsync(ct);
        return models.Select(m => m.Name).ToList();
    }

    public IModelGateway GetGateway(string provider) => _gatewayResolver.GetRequiredGateway(provider);
}