namespace KnowledgeAssistant.Application.Abstraction;

public interface INamedModelGateway : IModelGateway
{
    string Provider { get; }
}