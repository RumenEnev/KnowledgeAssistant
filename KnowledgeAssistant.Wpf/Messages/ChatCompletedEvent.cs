using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record ChatCompletedEvent : MessageBase
    {
        public ChatCompletedEvent(int promptTokens, int responseTokens)
        {
            PromptTokens = promptTokens;
            ResponseTokens = responseTokens;
        }

        public int PromptTokens { get; }

        public int ResponseTokens { get; }
    }
}