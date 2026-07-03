using KnowledgeAssistant.Domain.Conversation;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record ConversationUpdatedEvent: MessageBase
    {
        public ConversationUpdatedEvent(Conversation conversation)
        {
            Conversation = conversation;
        }

        public Conversation Conversation { get; }
    }
}