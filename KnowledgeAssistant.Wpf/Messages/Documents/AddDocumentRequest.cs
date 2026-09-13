using KnowledgeAssistant.Contracts.Enums;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record AddDocumentRequest : MessageBase
{
    public AddDocumentRequest(string title, string text, string embeddingModel, DocumentType documentType, IEnumerable<string> topics)
    {
        Title = title;
        Text = text;
        EmbeddingModel = embeddingModel;
        DocumentType = documentType;
        Topics = topics;
    }

    public string Title { get; }

    public string Text { get; }

    public string EmbeddingModel { get; }

    public DocumentType DocumentType { get; }

    public IEnumerable<string> Topics { get; }
}