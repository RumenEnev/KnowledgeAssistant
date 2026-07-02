using KnowledgeAssistant.Wpf.Models;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record UpdateConversationMessages : MessageBase
    {
        public UpdateConversationMessages(ConversationCompositionModel conversation)
        {
            Conversation = conversation;
        }

        public ConversationCompositionModel Conversation { get; }
    }
}