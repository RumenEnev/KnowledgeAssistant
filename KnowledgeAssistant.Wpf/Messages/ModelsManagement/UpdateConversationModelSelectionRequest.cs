using MessageServices;

public record UpdateConversationModelSelectionRequest : MessageBase
{
    public UpdateConversationModelSelectionRequest(Guid conversationId, string selectedProvider, string selectedModel)
    {
        ConversationId = conversationId;
        SelectedProvider = selectedProvider;
        SelectedModel = selectedModel;
    }

    public Guid ConversationId { get; }

    public string SelectedProvider { get; }

    public string SelectedModel { get; }
}