using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documentation
{
    public record DocumentationReadyEvent : MessageBase
    {
        public DocumentationReadyEvent(string outputPath, string title)
        {
            OutputPath = outputPath;
            Title = title;
        }

        public string OutputPath { get; }

        public string Title { get; }
    }
}