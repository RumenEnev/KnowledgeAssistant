using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record ChatCompletedEvent : MessageBase
    {
        public ChatCompletedEvent(int prompTokens, int responseTokens)
        {
            PrompTokens = prompTokens;
            ResponseTokens = responseTokens;
        }

        public int PrompTokens { get; }

        public int ResponseTokens { get; }
    }
}