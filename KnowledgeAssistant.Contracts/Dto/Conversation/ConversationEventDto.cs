namespace KnowledgeAssistant.Contracts.Dto.Conversation;

public record ConversationEventDto
{
    public required string Type { get; init; } 

    public string? Content { get; init; }

    public Guid? ConversationId { get; init; }

    public string? Title { get; init; }
}