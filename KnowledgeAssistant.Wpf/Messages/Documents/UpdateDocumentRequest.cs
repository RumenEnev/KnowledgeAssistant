using KnowledgeAssistant.Contracts.Enums;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record UpdateDocumentRequest : MessageBase
{
    public UpdateDocumentRequest(int documentId, string title, string text, string embeddingModel, DocumentType documentType, IEnumerable<string> topics)
    {
        DocumentId = documentId;
        Title = title;
        Text = text;
        EmbeddingModel = embeddingModel;
        DocumentType = documentType;
        Topics = topics;
    }

    public int DocumentId { get; }

    public string Title { get; }

    public string Text { get; }

    public string EmbeddingModel { get; }

    public DocumentType DocumentType { get; }

    public IEnumerable<string> Topics { get; }
}