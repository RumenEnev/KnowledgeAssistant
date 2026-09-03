using KnowledgeAssistant.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KnowledgeAssistant.Infrastructure;

public static class AdessoAiHubServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AI Hub concrete gateways. Use this when Ollama and AI Hub
    /// must coexist and your own resolver chooses a provider.
    /// </summary>
    public static IServiceCollection AddAdessoAiHub(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AdessoAiHubOptions>()
            .Bind(configuration.GetSection(AdessoAiHubOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
                "AdessoAiHub:BaseUrl must be an absolute URL.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "AdessoAiHub:ApiKey is required.")
            .ValidateOnStart();

        services.AddHttpClient<AdessoAiHubModelGateway>(ConfigureClient);
        services.AddHttpClient<AdessoAiHubModelCatalogGateway>(ConfigureClient);

        return services;
    }

    /// <summary>
    /// Registers AI Hub and exposes it through the existing IModelGateway and
    /// IModelCatalogGateway interfaces. Call this instead of the Ollama default
    /// registrations when AI Hub should be the active provider.
    /// </summary>
    public static IServiceCollection AddAdessoAiHubAsDefaultModelProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAdessoAiHub(configuration);
        services.AddTransient<IModelGateway>(provider =>
            provider.GetRequiredService<AdessoAiHubModelGateway>());
        services.AddTransient<INamedModelCatalogGateway>(provider =>
            provider.GetRequiredService<AdessoAiHubModelCatalogGateway>());

        return services;
    }

    private static void ConfigureClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<AdessoAiHubOptions>>().Value;
        var baseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? options.BaseUrl
            : options.BaseUrl + "/";

        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        client.Timeout = options.Timeout;
    }
}
