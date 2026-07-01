using KnowledgeAssistant.Domain.Conversation;

namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IConversationRepository
    {
        Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken cancellationToken);

        Task<Conversation?> GetAsync(Guid id, CancellationToken cancellationToken);

        Task CreateAsync(Conversation conversation, CancellationToken cancellationToken);

        Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    }
}