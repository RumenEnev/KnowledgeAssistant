using KnowledgeAssistant.Contracts.Enums;

namespace RagEvaluation.Desktop.Models;

public sealed record IngestOptions(string EmbeddingModel, int ChunkSize, int ChunkOverlap, DocumentType DocumentType, IReadOnlyList<string> TopicNames, bool AllDocuments);

public sealed record PipelineStatus(int Documents, int Chunks, int TestQuestions, string? LastRunName, double? LastRunRecall, string? LastRunWhen);

public sealed record StatusItem(string Label, string Value, string? ToolTip = null);