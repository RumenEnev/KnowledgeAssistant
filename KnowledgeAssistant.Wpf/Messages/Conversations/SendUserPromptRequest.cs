using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record SendUserPromptRequest : MessageBase
    {
        public SendUserPromptRequest(string prompt, string model, IEnumerable<string> chatHistory)
        {
            Prompt = prompt;
            Model = model;
            ChatHistory = chatHistory;
        }

        public string Prompt { get; }

        public string Model { get; }

        public IEnumerable<string> ChatHistory { get; }
    }
}