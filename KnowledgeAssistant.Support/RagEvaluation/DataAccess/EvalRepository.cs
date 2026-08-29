using Dapper;
using KnowledgeAssistant.Domain.Documents;
using Npgsql;
using NpgsqlTypes;
using RagEvaluation.Enums;
using RagEvaluation.Interfaces;
using RagEvaluation.Models;
using System.Text.Json;

namespace KnowledgeAssistant.Eval.Infrastructure;

public sealed class EvalRepository : IExperimentRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public EvalRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<List<TestQuery>> SaveTestQueriesAsync(IEnumerable<NewTestQuery> queries, CancellationToken ct = default)
    {
        var saved = new List<TestQuery>();

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        const string insertQuery = """
            INSERT INTO rag.eval_queries (query_text, query_type, topic_id, source_document_id, expected_answer)
            VALUES (@QueryText, @QueryType, @TopicId, @SourceDocumentId, @ExpectedAnswer)
            RETURNING id;
            """;

        const string insertExpected = """
            INSERT INTO rag.eval_query_expected_chunks (query_id, chunk_id)
            VALUES (@QueryId, @ChunkId)
            ON CONFLICT DO NOTHING;
            """;

        foreach (var q in queries)
        {
            var id = await connection.QuerySingleAsync<int>(insertQuery, new
            {
                QueryText = q.QueryText,
                QueryType = q.Type.ToString(),
                TopicId = q.TopicId,
                SourceDocumentId = q.SourceDocumentId,
                ExpectedAnswer = q.ExpectedAnswer
            }, tx);

            foreach (var chunkId in q.ExpectedChunkIds)
            {
                await connection.ExecuteAsync(insertExpected, new { QueryId = id, ChunkId = chunkId }, tx);
            }

            saved.Add(new TestQuery
            {
                Id = id,
                QueryText = q.QueryText,
                Type = q.Type,
                TopicId = q.TopicId,
                SourceDocumentId = q.SourceDocumentId,
                ExpectedAnswer = q.ExpectedAnswer,
                ExpectedChunkIds = q.ExpectedChunkIds
            });
        }

        await tx.CommitAsync(ct);
        return saved;
    }

    public async Task<List<TestQuery>> LoadTestQueriesAsync(CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT q.id, q.query_text AS QueryText, q.query_type AS QueryType, q.topic_id AS TopicId,
                   q.source_document_id AS SourceDocumentId, q.expected_answer AS ExpectedAnswer,
                   COALESCE(array_agg(ec.chunk_id) FILTER (WHERE ec.chunk_id IS NOT NULL), '{}') AS ExpectedChunkIds
            FROM rag.eval_queries q
            LEFT JOIN rag.eval_query_expected_chunks ec ON ec.query_id = q.id
            GROUP BY q.id
            ORDER BY q.id;
            """;

        var rows = await connection.QueryAsync<EvalQueryRow>(sql);

        return rows.Select(r => new TestQuery
        {
            Id = r.Id,
            QueryText = r.QueryText,
            Type = Enum.Parse<QueryType>(r.QueryType),
            TopicId = r.TopicId,
            SourceDocumentId = r.SourceDocumentId,
            ExpectedAnswer = r.ExpectedAnswer,
            ExpectedChunkIds = (r.ExpectedChunkIds ?? Array.Empty<int>()).ToList()
        }).ToList();
    }

    public async Task<ExperimentRun> SaveRunAsync(ExperimentRun run, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO rag.eval_runs (run_name, chunking_config, chat_model, embedding_model, judge_model, notes, created_at)
            VALUES (@run_name, @chunking_config, @chat_model, @embedding_model, @judge_model, @notes, @created_at)
            RETURNING id;
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("run_name", run.RunName);
        cmd.Parameters.Add(new NpgsqlParameter("chunking_config", NpgsqlDbType.Jsonb) { Value = run.ChunkingConfigNotes });
        cmd.Parameters.AddWithValue("chat_model", run.ChatModel);
        cmd.Parameters.AddWithValue("embedding_model", run.EmbeddingModel);
        cmd.Parameters.AddWithValue("judge_model", run.JudgeModel);
        cmd.Parameters.AddWithValue("notes", (object?)run.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("created_at", run.CreatedAt);

        var id = (int)(await cmd.ExecuteScalarAsync(ct))!;

        return new ExperimentRun
        {
            Id = id,
            RunName = run.RunName,
            ChatModel = run.ChatModel,
            EmbeddingModel = run.EmbeddingModel,
            JudgeModel = run.JudgeModel,
            ChunkingConfigNotes = run.ChunkingConfigNotes,
            CreatedAt = run.CreatedAt,
            Notes = run.Notes
        };
    }

    public async Task SaveRetrievalResultAsync(int runId, int queryId, IReadOnlyList<SelectedChunk> candidates, RetrievalMetrics metrics, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        const string insertCandidate = """
            INSERT INTO rag.eval_retrieval_results (run_id, query_id, chunk_id, rank, included_in_budget, approx_tokens)
            VALUES (@RunId, @QueryId, @ChunkId, @Rank, @IncludedInBudget, @ApproxTokens);
            """;

        foreach (var c in candidates)
        {
            await connection.ExecuteAsync(insertCandidate, new
            {
                RunId = runId,
                QueryId = queryId,
                ChunkId = c.ChunkId,
                Rank = c.Rank,
                IncludedInBudget = c.IncludedInBudget,
                ApproxTokens = c.ApproxTokens
            }, tx);
        }

        const string insertMetrics = """
            INSERT INTO rag.eval_retrieval_metrics (run_id, query_id, precision_at_k, recall_at_k, reciprocal_rank, ndcg_at_k)
            VALUES (@RunId, @QueryId, @Precision, @Recall, @Rr, @Ndcg);
            """;

        await connection.ExecuteAsync(insertMetrics, new
        {
            RunId = runId,
            QueryId = queryId,
            Precision = metrics.PrecisionAtK,
            Recall = metrics.RecallAtK,
            Rr = metrics.ReciprocalRank,
            Ndcg = metrics.NdcgAtK
        }, tx);

        await tx.CommitAsync(ct);
    }

    public async Task SaveGenerationResultAsync(GenerationResult result, GenerationMetrics metrics, string judgePromptVersion, string judgeModel, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        const string insertResult = """
            INSERT INTO rag.eval_generation_results (run_id, query_id, generated_answer, context_chunk_ids)
            VALUES (@run_id, @query_id, @answer, @context_ids)
            RETURNING id;
            """;

        int generationResultId;
        await using (var cmd = new NpgsqlCommand(insertResult, connection, tx))
        {
            cmd.Parameters.AddWithValue("run_id", result.RunId);
            cmd.Parameters.AddWithValue("query_id", result.QueryId);
            cmd.Parameters.AddWithValue("answer", result.GeneratedAnswer);
            cmd.Parameters.Add(new NpgsqlParameter("context_ids", NpgsqlDbType.Jsonb)
            {
                Value = JsonSerializer.Serialize(result.ContextChunkIds)
            });
            generationResultId = (int)(await cmd.ExecuteScalarAsync(ct))!;
        }

        const string insertMetrics = """
            INSERT INTO rag.eval_generation_metrics
                (generation_result_id, faithfulness_score, relevance_score, completeness_score, judge_model, judge_prompt_version, judge_rationale)
            VALUES (@id, @faithfulness, @relevance, @completeness, @judge_model, @version, @rationale);
            """;

        await connection.ExecuteAsync(insertMetrics, new
        {
            id = generationResultId,
            faithfulness = metrics.FaithfulnessScore,
            relevance = metrics.RelevanceScore,
            completeness = metrics.CompletenessScore,
            judge_model = judgeModel,
            version = judgePromptVersion,
            rationale = metrics.JudgeRationale
        }, tx);

        await tx.CommitAsync(ct);
    }

    public async Task<RunSummary> GetRunSummaryAsync(int runId, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        const string runSql = """
            SELECT id, run_name AS RunName, chunking_config::text AS ChunkingConfigNotes,
                   chat_model AS ChatModel, embedding_model AS EmbeddingModel, judge_model AS JudgeModel,
                   notes, created_at AS CreatedAt
            FROM rag.eval_runs WHERE id = @RunId;
            """;

        var run = await connection.QuerySingleOrDefaultAsync<ExperimentRun>(runSql, new { RunId = runId })
            ?? throw new InvalidOperationException($"Run {runId} not found.");

        const string metricsSql = """
            SELECT
                COALESCE(AVG(rm.precision_at_k), 0) AS MeanPrecisionAtK,
                COALESCE(AVG(rm.recall_at_k), 0) AS MeanRecallAtK,
                COALESCE(AVG(rm.reciprocal_rank), 0) AS MeanReciprocalRank,
                COALESCE(AVG(rm.ndcg_at_k), 0) AS MeanNdcgAtK,
                COALESCE((SELECT AVG(faithfulness_score) FROM rag.eval_generation_metrics gm
                          JOIN rag.eval_generation_results gr ON gr.id = gm.generation_result_id
                          WHERE gr.run_id = @RunId), 0) AS MeanFaithfulness,
                COALESCE((SELECT AVG(relevance_score) FROM rag.eval_generation_metrics gm
                          JOIN rag.eval_generation_results gr ON gr.id = gm.generation_result_id
                          WHERE gr.run_id = @RunId), 0) AS MeanRelevance,
                COALESCE((SELECT AVG(completeness_score) FROM rag.eval_generation_metrics gm
                          JOIN rag.eval_generation_results gr ON gr.id = gm.generation_result_id
                          WHERE gr.run_id = @RunId), 0) AS MeanCompleteness
            FROM rag.eval_retrieval_metrics rm
            WHERE rm.run_id = @RunId;
            """;

        var metrics = await connection.QuerySingleAsync<RunSummaryMetricsRow>(metricsSql, new { RunId = runId });

        return new RunSummary
        {
            Run = run,
            MeanPrecisionAtK = metrics.MeanPrecisionAtK,
            MeanRecallAtK = metrics.MeanRecallAtK,
            MeanReciprocalRank = metrics.MeanReciprocalRank,
            MeanNdcgAtK = metrics.MeanNdcgAtK,
            MeanFaithfulness = metrics.MeanFaithfulness,
            MeanRelevance = metrics.MeanRelevance,
            MeanCompleteness = metrics.MeanCompleteness
        };
    }

    public async Task<List<ExperimentRun>> ListRunsAsync(CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        const string sql = """
            SELECT id, run_name AS RunName, chunking_config::text AS ChunkingConfigNotes,
                   chat_model AS ChatModel, embedding_model AS EmbeddingModel, judge_model AS JudgeModel,
                   notes, created_at AS CreatedAt
            FROM rag.eval_runs
            ORDER BY created_at DESC;
            """;

        var rows = await connection.QueryAsync<ExperimentRun>(sql);
        return rows.ToList();
    }

    public async Task CleanDatabaseAsync(CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        const string sql = """
            TRUNCATE TABLE
                rag.eval_generation_metrics,
                rag.eval_generation_results,
                rag.eval_retrieval_metrics,
                rag.eval_retrieval_results,
                rag.eval_runs,
                rag.eval_query_expected_chunks,
                rag.eval_queries
            RESTART IDENTITY CASCADE;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<List<QueryMetricsRow>> GetQueryMetricsAsync(int runId, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        const string sql = """
            SELECT
                q.id AS QueryId,
                q.query_text AS QueryText,
                rm.precision_at_k AS PrecisionAtK,
                rm.recall_at_k AS RecallAtK,
                rm.reciprocal_rank AS ReciprocalRank,
                rm.ndcg_at_k AS NdcgAtK,
                gm.faithfulness_score AS FaithfulnessScore,
                gm.relevance_score AS RelevanceScore,
                gm.completeness_score AS CompletenessScore
            FROM rag.eval_queries q
            LEFT JOIN rag.eval_retrieval_metrics rm ON rm.query_id = q.id AND rm.run_id = @RunId
            LEFT JOIN rag.eval_generation_results gr ON gr.query_id = q.id AND gr.run_id = @RunId
            LEFT JOIN rag.eval_generation_metrics gm ON gm.generation_result_id = gr.id
            WHERE rm.run_id = @RunId OR gr.run_id = @RunId
            ORDER BY q.id;
            """;

        var rows = await connection.QueryAsync<QueryMetricsRow>(sql, new { RunId = runId });
        return rows.ToList();
    }
}