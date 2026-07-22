using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelContextWindows
{
    public record ModelContextWindowsUpdatedEvent : MessageBase
    {
        public ModelContextWindowsUpdatedEvent(IReadOnlyList<ModelContextWindowInfo> models)
        {
            Models = models;
        }

        public IReadOnlyList<ModelContextWindowInfo> Models { get; }
    }
}
