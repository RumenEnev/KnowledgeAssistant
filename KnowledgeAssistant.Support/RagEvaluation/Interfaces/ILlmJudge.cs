using KnowledgeAssistant.Application.Abstraction;
using RagEvaluation.Models;

namespace RagEvaluation.Interfaces;

public interface ILlmJudge
{
    Task<GenerationMetrics> ScoreAsync(IModelGateway modelGateway, string judgeModel, TestQuery query, GenerationResult result, IReadOnlyList<KnowledgeAssistant.Domain.Documents.DocumentChunk> contextChunks, CancellationToken ct = default);
}