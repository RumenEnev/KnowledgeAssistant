namespace KnowledgeAssistant.Contracts.Dto.Conversation;

public record CreateConversationDto
{
    public required string Provider { get; init; }

    public required string Model { get; init; }
}