using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain.Conversation;
using RagEvaluation.Enums;
using RagEvaluation.Interfaces;
using RagEvaluation.Models;
using System.Text.Json;

namespace RagEvaluation.Services;

public sealed class TestSetGenerationService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IModelGateway _modelGateway;
    private readonly IExperimentRepository _experimentRepository;

    public TestSetGenerationService(IDocumentRepository documentRepository, IModelGateway modelGateway, IExperimentRepository experimentRepository)
    {
        _documentRepository = documentRepository;
        _modelGateway = modelGateway;
        _experimentRepository = experimentRepository;
    }

    public async Task<int> GenerateAsync(string generatorModel, int questionsPerChunk, CancellationToken ct = default)
    {
        var allTopics = await _documentRepository.GetAllTopicsAsync(ct);
        var topicIdByName = allTopics.ToDictionary(t => t.Name, t => t.Id);

        var documents = await _documentRepository.GetAllDocumentsAsync(ct);
        var newQueries = new List<NewTestQuery>();
        int chunksCounter = 0;
        int documentCounter = 0;
        foreach (var document in documents)
        {
            var documentTopicIds = document.Topics
                .Where(topicIdByName.ContainsKey)
                .Select(name => topicIdByName[name])
                .Distinct()
                .ToList();

            if (documentTopicIds.Count == 0)
            {
                continue;
            }

            documentCounter++;
            var chunks = await _documentRepository.GetChunksByDocumentIdAsync(document.Id, ct);
            foreach (var chunk in chunks)
            {
                chunksCounter++;
                Console.WriteLine($"Generating questions for chunk #: {chunksCounter}; Document #: {documentCounter} of {documents.Count()}");
                var questions = await GenerateQuestionsAsync(generatorModel, chunk.ChunkText, questionsPerChunk, ct);
                foreach (var question in questions)
                {
                    foreach (var topicId in documentTopicIds)
                    {
                        newQueries.Add(new NewTestQuery
                        {
                            QueryText = question,
                            Type = QueryType.SingleChunk,
                            TopicId = topicId,
                            SourceDocumentId = document.Id,
                            ExpectedChunkIds = new List<int> { chunk.Id }
                        });
                    }
                }
            }
        }

        var saved = await _experimentRepository.SaveTestQueriesAsync(newQueries, ct);
        return saved.Count;
    }

    private async Task<List<string>> GenerateQuestionsAsync(string model, string chunkText, int count, CancellationToken ct)
    {
        var systemMessage = new ChatMessage
        {
            Role = "system",
            Content = """
                You write test questions for a retrieval evaluation benchmark.
                Given a single passage, write questions that can ONLY be answered
                using information in that passage - avoid vague or generic questions.
                Respond with ONLY a JSON array of strings, no markdown fences, no other text.
                """
        };

        var userMessage = new ChatMessage
        {
            Role = "user",
            Content = $"Number of questions: {count}\n\nPassage:\n{chunkText}"
        };

        var raw = await _modelGateway.GenerateAsync(model, userMessage, systemMessage, ct);
        try
        {
            var jsonStart = raw.IndexOf('[');
            var jsonEnd = raw.LastIndexOf(']');
            if (jsonStart < 0 || jsonEnd < jsonStart) return new List<string>();
            return JsonSerializer.Deserialize<List<string>>(raw[jsonStart..(jsonEnd + 1)]) ?? new();
        }
        catch (JsonException)
        {
            return new List<string>(); // caller just gets fewer questions for this chunk, not a hard failure
        }
    }
}