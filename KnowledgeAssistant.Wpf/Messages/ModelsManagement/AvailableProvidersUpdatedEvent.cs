using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelsManagement;

public record AvailableProvidersUpdatedEvent : MessageBase
{
    public AvailableProvidersUpdatedEvent(IEnumerable<string> providers)
    {
        Providers = providers.ToArray();
    }

    public IReadOnlyCollection<string> Providers { get; }
}