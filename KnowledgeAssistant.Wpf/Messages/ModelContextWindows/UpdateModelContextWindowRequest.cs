using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelContextWindows;

public record UpdateModelContextWindowRequest : MessageBase
{
    public UpdateModelContextWindowRequest(Guid id, bool internalUseOnly, bool canCallTools, bool isFavorite)
    {
        Id = id;
        InternalUseOnly = internalUseOnly;
        CanCallTools = canCallTools;
        IsFavorite = isFavorite;
    }

    public Guid Id { get; }

    public bool InternalUseOnly { get; }

    public bool CanCallTools { get; }

    public bool IsFavorite { get; }
}