using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages;

public record GenerateTitleRequest : MessageBase
{
    public GenerateTitleRequest(string userPrompt, string provider, string model, Guid conversationId)
    {
        UserPrompt = userPrompt;
        Provider = provider;
        Model = model;
        ConversationId = conversationId;
    }

    public string UserPrompt { get; }

    public string Provider { get; }

    public string Model { get; }

    public Guid ConversationId { get; }
}