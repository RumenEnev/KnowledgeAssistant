using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.SpeechToText;

public record SpeechToTextErrorRequest : MessageBase
{
    public SpeechToTextErrorRequest(string errorMessage)
    {
        ErrorMessage = errorMessage;
    }

    public string ErrorMessage { get; set; }
}