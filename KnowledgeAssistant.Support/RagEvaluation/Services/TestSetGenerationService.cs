using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain.Conversation;
using RagEvaluation.Enums;
using RagEvaluation.Interfaces;
using RagEvaluation.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RagEvaluation.Services;

public sealed class TestSetGenerationService
{
    public const string PromptVersion = "v2";

    private const int MinChunkChars = 150;
    private const int MinQuestionChars = 15;

    private static readonly Regex MetaReference = new(
        @"\b(the|this|that)\s+(passage|text|document|article|excerpt|section|paragraph|author|context)\b|\baccording to\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IDocumentRepository _documentRepository;
    private readonly IExperimentRepository _experimentRepository;

    public TestSetGenerationService(IDocumentRepository documentRepository, IExperimentRepository experimentRepository)
    {
        _documentRepository = documentRepository;
        _experimentRepository = experimentRepository;
    }

    private sealed record DocumentWork(
        int DocumentId,
        IReadOnlyList<KnowledgeAssistant.Domain.Documents.DocumentChunk> Chunks,
        IReadOnlyList<int> TopicIds);

    public async Task<int> GenerateAsync(
        IModelGateway modelGateway,
        string provider,
        string generatorModel,
        int questionsPerChunk,
        int? documentId = null,
        IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default,
        bool replaceExisting = true)
    {
        var allTopics = await _documentRepository.GetAllTopicsAsync(ct);
        var topicIdByName = allTopics.ToDictionary(t => t.Name, t => t.Id);
        var documents = await _documentRepository.GetAllDocumentsAsync(ct);
        var eligibleDocuments = documents
            .Where(d => d.Topics.Any(topicIdByName.ContainsKey))
            .Where(d => documentId is null || d.Id == documentId.Value)
            .ToList();

        var work = new List<DocumentWork>();
        foreach (var document in eligibleDocuments)
        {
            ct.ThrowIfCancellationRequested();

            if (!replaceExisting && (await _experimentRepository.LoadTestQueriesAsync(document.Id, ct)).Any())
            {
                continue;
            }

            var chunks = (await _documentRepository.GetChunksByDocumentIdAsync(document.Id, ct))
                .Where(c => c.ChunkText.Length >= MinChunkChars)
                .OrderBy(c => c.ChunkIndex)
                .ToList();

            var topicIds = document.Topics
                .Where(topicIdByName.ContainsKey)
                .Select(name => topicIdByName[name])
                .Distinct()
                .ToList();

            if (chunks.Count > 0 && topicIds.Count > 0)
            {
                work.Add(new DocumentWork(document.Id, chunks, topicIds));
            }
        }

        var totalChunks = work.Sum(w => w.Chunks.Count);
        var chunksDone = 0;
        var saved = 0;
        foreach (var item in work)
        {
            var newQueries = new List<NewTestQuery>();
            foreach (var chunk in item.Chunks)
            {
                ct.ThrowIfCancellationRequested();
                chunksDone++;
                progress?.Report((chunksDone, totalChunks));

                var questions = await GenerateQuestionsAsync(modelGateway, generatorModel, chunk.ChunkText, questionsPerChunk, ct);
                foreach (var question in questions)
                {
                    foreach (var topicId in item.TopicIds)
                    {
                        newQueries.Add(new NewTestQuery
                        {
                            QueryText = question,
                            Type = QueryType.SingleChunk,
                            TopicId = topicId,
                            SourceDocumentId = item.DocumentId,
                            ExpectedChunkIds = new List<int> { chunk.Id },
                            GeneratorProvider = provider,
                            GeneratorModel = generatorModel
                        });
                    }
                }
            }

            if (newQueries.Count == 0)
            {
                continue; 
            }

            ct.ThrowIfCancellationRequested();
            if (replaceExisting)
            {
                await _experimentRepository.DeleteTestQueriesByDocumentIdsAsync(new[] { item.DocumentId }, ct);
            }

            var savedQueries = await _experimentRepository.SaveTestQueriesAsync(newQueries, ct);
            saved += savedQueries.Count;
        }

        progress?.Report((totalChunks, totalChunks));
        return saved;
    }

    private async Task<List<string>> GenerateQuestionsAsync(IModelGateway modelGateway, string model, string chunkText, int count, CancellationToken ct)
    {
        var systemMessage = new ChatMessage
        {
            Role = "system",
            Content = """
                You write test questions for a retrieval benchmark. Each question will be sent to a
                search system that has to find the passage below, so it must read like something a
                real user would type WITHOUT having seen the passage.

                Rules:
                - Every question must be answerable from the passage alone.
                - Make each question self-contained: name the specific product, feature, person or
                  concept it is about, so it makes sense with no other context.
                - Never refer to "the passage", "the text", "the document", "the author", "this section"
                  or anything similar.
                - Do not copy long phrases from the passage; use the natural wording a user would choose.
                - Vary the style (what / how / why / when / which) across the questions.
                - Skip details that only matter inside the passage (page numbers, names of example variables).

                Respond with ONLY a JSON array of strings, no markdown fences, no other text.
                """
        };

        var userMessage = new ChatMessage
        {
            Role = "user",
            Content = $"Number of questions: {count}\n\nPassage:\n{chunkText}"
        };

        var raw = await modelGateway.GenerateAsync(model, userMessage, systemMessage, ct);
        List<string> parsed;
        try
        {
            var jsonStart = raw.IndexOf('[');
            var jsonEnd = raw.LastIndexOf(']');
            if (jsonStart < 0 || jsonEnd < jsonStart)
            {
                return new List<string>();
            }

            parsed = JsonSerializer.Deserialize<List<string>>(raw[jsonStart..(jsonEnd + 1)]) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>(); 
        }

        return parsed
            .Select(q => q?.Trim() ?? string.Empty)
            .Where(q => q.Length >= MinQuestionChars && !MetaReference.IsMatch(q))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToList();
    }
}