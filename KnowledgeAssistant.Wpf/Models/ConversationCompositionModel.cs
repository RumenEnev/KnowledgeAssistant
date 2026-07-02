using KnowledgeAssistant.Domain.Conversation;

namespace KnowledgeAssistant.Wpf.Models
{
    public class ConversationCompositionModel
    {
        public Guid Id { get; set; }

        public string? Title { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime UpdatedOn { get; set; }

        public IEnumerable<ChatMessage>? Messages { get; set; }
    }
}