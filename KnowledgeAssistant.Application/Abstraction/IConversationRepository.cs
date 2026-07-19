using KnowledgeAssistant.Domain.Conversation;

namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IConversationRepository
    {
        Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken cancellationToken);

        Task<Conversation?> GetAsync(Guid id, CancellationToken cancellationToken);

        Task CreateAsync(Conversation conversation, CancellationToken cancellationToken);

        Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken);

        Task CreateMessageAsync(Guid conversationId, ChatMessage message, CancellationToken cancellationToken);
    
        Task<Guid> DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken);

        Task<ChatMessage?> GetLastAssistantMessageAsync(Guid conversationId, CancellationToken cancellationToken);

        Task UpdateSelectedModelAsync(Guid conversationId, Guid modelId, CancellationToken cancellationToken);
    }
}