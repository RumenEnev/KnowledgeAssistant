namespace KnowledgeAssistant.Contracts.Dto;

public class ConversationDto
{
    public Guid Id { get; set; }

    public string? Title { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? SelectedProvider { get; set; }

    public string? SelectedModel { get; set; }

    public int? TopicId { get; set; }

    public string? Topic { get; set; }

    public IEnumerable<MessageDto>? Messages { get; set; }
}