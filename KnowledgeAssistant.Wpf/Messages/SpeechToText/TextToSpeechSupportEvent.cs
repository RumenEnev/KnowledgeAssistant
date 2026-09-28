using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.SpeechToText;

public record TextToSpeechSupportEvent : MessageBase
{
    public TextToSpeechSupportEvent(bool isSupported)
    {
        IsSupported = isSupported;
    }

    public bool IsSupported { get; }
}