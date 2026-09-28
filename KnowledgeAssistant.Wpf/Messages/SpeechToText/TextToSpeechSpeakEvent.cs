using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.SpeechToText;

public record TextToSpeechSpeakEvent : MessageBase
{
    public TextToSpeechSpeakEvent(string text)
    {
        Text = text;
    }

    public string Text { get; }
}