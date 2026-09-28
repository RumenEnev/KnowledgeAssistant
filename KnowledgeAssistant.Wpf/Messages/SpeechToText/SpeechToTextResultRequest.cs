using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.SpeechToText;

public record SpeechToTextResultRequest : MessageBase
{
    public SpeechToTextResultRequest(string voiceInputBaseText, string transcript, bool isFinal)
    {
        VoiceInputBaseText = voiceInputBaseText;
        Transcript = transcript;
        IsFinal = isFinal;
    }

    public string VoiceInputBaseText { get; }

    public string Transcript { get; }

    public bool IsFinal { get; }
}