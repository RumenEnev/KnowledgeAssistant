using KnowledgeAssistant.Contracts.Enums;

namespace RagEvaluation.Models;

public sealed record IngestOptions(string EmbeddingModel, int ChunkSize, int ChunkOverlap, DocumentType DocumentType, IReadOnlyList<string> TopicNames, bool AllDocuments);

public sealed record PipelineStatus(int Documents, int Chunks, int TestQuestions, string? LastRunName, double? LastRunRecall, string? LastRunWhen);

public sealed record StatusItem(string Label, string Value, string? ToolTip = null);

public sealed record DocumentChoice(int? Id, string Title);

public sealed record GenerateTestSetOptions(string Provider, string Model, int QuestionsPerChunk, int? DocumentId, bool SkipExisting);