namespace KnowledgeAssistant.Contracts.Dto.Conversation;

public record ChatResponseChunkDto
{
    public string Type { get; set; } = default!; 

    public string? Content { get; set; }

    public Guid? ConversationId { get; set; }

    public Guid? MessageId { get; set; }
}