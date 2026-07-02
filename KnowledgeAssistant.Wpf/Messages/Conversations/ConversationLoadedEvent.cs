using KnowledgeAssistant.Contracts.Dto;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record ConversationLoadedEvent : MessageBase
    {
        public ConversationLoadedEvent(ConversationDto dto)
        {
            Dto = dto;
        }

        public ConversationDto Dto { get; }
    }
}