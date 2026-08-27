using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Domain.Documents;
using RagEvaluation.Interfaces;
using RagEvaluation.Models;
using System.Text.Json;

namespace KnowledgeAssistant.Eval.Core.Judging;

public sealed class LlmJudge : ILlmJudge
{
    public const string JudgePromptVersion = "v1";

    private readonly IModelGateway _modelGateway;

    public LlmJudge(IModelGateway modelGateway)
    {
        _modelGateway = modelGateway;
    }

    public async Task<GenerationMetrics> ScoreAsync(
        string judgeModel,
        TestQuery query,
        GenerationResult result,
        IReadOnlyList<DocumentChunk> contextChunks,
        CancellationToken ct = default)
    {
        var contextText = string.Join("\n\n---\n\n", contextChunks.Select((c, i) => $"[Chunk {i + 1}]\n{c.ChunkText}"));
        const string systemPromptText = """
            You are a strict evaluator of RAG (retrieval-augmented generation) answers.
            You will be given a user query, the context chunks that were retrieved for
            that query, and an answer generated from that context.

            Score the answer on three dimensions, each from 1 to 5:
            - faithfulness: Does the answer ONLY make claims supported by the context?
              5 = fully grounded, no unsupported claims. 1 = mostly fabricated/unsupported.
            - relevance: Does the answer actually address the user's query?
              5 = directly and completely addresses it. 1 = off-topic or non-responsive.
            - completeness: Given what's IN the context, did the answer use the available
              relevant information? 5 = uses all relevant available info. 1 = ignores
              relevant info that was right there in the context.

            Respond with ONLY a JSON object, no markdown fences, no other text, in this
            exact shape:
            {"faithfulness": <1-5>, "relevance": <1-5>, "completeness": <1-5>, "rationale": "<one or two sentences>"}
            """;

        var userPromptText = $"""
            QUERY:
            {query.QueryText}

            CONTEXT CHUNKS:
            {contextText}

            GENERATED ANSWER:
            {result.GeneratedAnswer}
            """;

        var systemMessage = new ChatMessage { Role = "system", Content = systemPromptText };
        var userMessage = new ChatMessage { Role = "user", Content = userPromptText };

        var raw = await _modelGateway.GenerateAsync(judgeModel, userMessage, systemMessage, ct);
        return ParseJudgeResponse(query.Id, raw);
    }

    private static GenerationMetrics ParseJudgeResponse(int queryId, string raw)
    {
        var jsonStart = raw.IndexOf('{');
        var jsonEnd = raw.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd < jsonStart)
        {
            throw new InvalidOperationException($"Judge response was not parseable JSON: {raw}");
        }

        var json = raw[jsonStart..(jsonEnd + 1)];
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new GenerationMetrics
        {
            QueryId = queryId,
            FaithfulnessScore = root.GetProperty("faithfulness").GetDouble(),
            RelevanceScore = root.GetProperty("relevance").GetDouble(),
            CompletenessScore = root.GetProperty("completeness").GetDouble(),
            JudgeRationale = root.TryGetProperty("rationale", out var r) ? r.GetString() ?? "" : ""
        };
    }
}