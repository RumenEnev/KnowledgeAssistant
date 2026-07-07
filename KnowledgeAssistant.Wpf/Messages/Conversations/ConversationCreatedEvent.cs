using KnowledgeAssistant.Domain.Conversation;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record ConversationCreatedEvent : MessageBase
    {
        public ConversationCreatedEvent(Conversation conversation)
        {
            Conversation = conversation;
        }

        public Conversation Conversation { get; }
    }
}