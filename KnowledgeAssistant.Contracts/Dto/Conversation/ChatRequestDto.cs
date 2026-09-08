using KnowledgeAssistant.Contracts.Enums;

namespace KnowledgeAssistant.Contracts.Dto.Conversation;

public class ChatRequestDto
{
    public Guid? ConversationId { get; set; }

    public string Role { get; set; } = "user";

    public string Message { get; set; } = default!;

    public required string Model { get; set; }

    public required string Provider { get; set; }

    public double? Temperature { get; set; }

    public string SystemPromt { get; set; } = string.Empty;

    public MessageSource Source { get; set; } = MessageSource.Web;
}
