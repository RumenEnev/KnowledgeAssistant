using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record SendToolPromptRequest : MessageBase
    {
        public SendToolPromptRequest(string systemPrompt, string context)
        {
            SystemPrompt = systemPrompt;
            Context = context;
        }

        public string SystemPrompt { get; }

        public string Context { get; }
    }
}