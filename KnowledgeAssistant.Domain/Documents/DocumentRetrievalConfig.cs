namespace KnowledgeAssistant.Domain.Documents;

public sealed class DocumentRetrievalConfig
{
    public int DocumentId { get; set; }
    public int ChunkSize { get; set; }
    public int ChunkOverlap { get; set; }
    public int CandidatePoolSize { get; set; }
    public int CandidateFanout { get; set; }
    public double MaxDistanceThreshold { get; set; }
    public int RrfK { get; set; }
    public double TargetInjectionFraction { get; set; }
    public double MaxInjectionFraction { get; set; }

    public static DocumentRetrievalConfig Default(int documentId) => new()
    {
        DocumentId = documentId,
        ChunkSize = 1200,
        ChunkOverlap = 200,
        CandidatePoolSize = 5,
        CandidateFanout = 20,
        MaxDistanceThreshold = 0.5,
        RrfK = 60,
        TargetInjectionFraction = 0.30,
        MaxInjectionFraction = 0.50
    };
}