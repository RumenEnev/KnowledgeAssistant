using KnowledgeAssistant.Contracts.Enums;

namespace KnowledgeAssistant.Contracts.Dto.Conversation;

public class ChatRequestDto
{
    public Guid? ConversationId { get; set; }

    public string Role { get; set; } = "user";

    public string Message { get; set; } = default!;

    public string? Model { get; set; }

    public string? Provider { get; set; }

    public double? Temperature { get; set; }

    public string SystemPromt { get; set; } = string.Empty;

    public MessageSource Source { get; set; } = MessageSource.Web;
}
