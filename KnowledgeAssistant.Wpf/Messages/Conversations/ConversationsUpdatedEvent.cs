using KnowledgeAssistant.Domain.Conversation;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record ConversationsUpdatedEvent : MessageBase
    {
        public ConversationsUpdatedEvent(IEnumerable<Conversation> conversations)
        {
            Conversations = conversations;
        }

        public IEnumerable<Conversation> Conversations { get; }
    }
}