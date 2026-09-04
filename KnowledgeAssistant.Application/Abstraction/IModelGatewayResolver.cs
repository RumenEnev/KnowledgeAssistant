namespace KnowledgeAssistant.Application.Abstraction;

public interface IModelGatewayResolver
{
    IReadOnlyCollection<string> Providers { get; }

    IModelGateway GetRequiredGateway(string provider);
}