using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record ChunkReceivedEvent : MessageBase
    {
        public ChunkReceivedEvent(string content)
        {
            Content = content;
        }

        public string Content { get; }
    }
}