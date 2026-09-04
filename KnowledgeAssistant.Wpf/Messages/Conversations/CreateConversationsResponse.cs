using KnowledgeAssistant.Contracts.Dto.Conversation;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record CreateConversationsResponse : MessageBase
    {
        public CreateConversationsResponse(ConversationDto conversation)
        {
            Conversation = conversation;
        }

        public ConversationDto Conversation { get; }
    }
}