using Dapper;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain.Documents;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KnowledgeAssistant.Application.Services;

public class DocumentRepository : IDocumentRepository
{
    private static readonly bool _typeHandlerRegistered = RegisterTypeHandler();
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<DocumentRepository> _logger;

    public DocumentRepository(NpgsqlDataSource dataSource, ILogger<DocumentRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    private static bool RegisterTypeHandler()
    {
        SqlMapper.AddTypeHandler(new VectorTypeHandler());
        return true;
    }

    public async Task<IEnumerable<Topic>> GetAllTopicsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = "SELECT id, name, parent_id AS ParentId FROM rag.topics ORDER BY name";
        return await connection.QueryAsync<Topic>(query);
    }

    public async Task<int?> GetTopicIdByNameAsync(string topicName, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = "SELECT id FROM rag.topics WHERE name = @TopicName";
        return await connection.QuerySingleOrDefaultAsync<int?>(query, new { TopicName = topicName });
    }

    public async Task<Topic> CreateTopicAsync(string name, int? parentId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            var query = """
                INSERT INTO rag.topics (name, parent_id)
                VALUES (@Name, @ParentId)
                RETURNING id, name, parent_id AS ParentId;
                """;
            return await connection.QuerySingleAsync<Topic>(query, new { Name = name, ParentId = parentId });
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException($"A topic named '{name}' already exists.");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new InvalidOperationException("The selected parent topic no longer exists.");
        }
    }

    public async Task<bool> UpdateTopicAsync(int topicId, string name, int? parentId, CancellationToken cancellationToken)
    {
        if (parentId == topicId)
        {
            throw new InvalidOperationException("A topic cannot be its own parent.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            var query = "UPDATE rag.topics SET name = @Name, parent_id = @ParentId WHERE id = @TopicId";
            var rows = await connection.ExecuteAsync(query, new { TopicId = topicId, Name = name, ParentId = parentId });
            return rows > 0;
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException($"A topic named '{name}' already exists.");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new InvalidOperationException("The selected parent topic no longer exists.");
        }
    }

    public async Task DeleteTopicAsync(int topicId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync("DELETE FROM rag.topics WHERE id = @TopicId", new { TopicId = topicId });
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new InvalidOperationException("This topic is still in use by one or more documents or conversations and cannot be deleted.");
        }
    }

    public async Task<int> CreateDocumentAsync(string title, string originalText, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = """
            INSERT INTO rag.documents (title, original_text, created_at)
            VALUES (@Title, @OriginalText, @CreatedAt)
            RETURNING id;
            """;

        return await connection.QuerySingleAsync<int>(query, new
        {
            Title = title,
            OriginalText = originalText,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task LinkDocumentTopicsAsync(int documentId, IEnumerable<int> topicIds, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = """
            INSERT INTO rag.document_topics (document_id, topic_id)
            VALUES (@DocumentId, @TopicId)
            ON CONFLICT DO NOTHING;
            """;

        foreach (var topicId in topicIds)
        {
            await connection.ExecuteAsync(query, new { DocumentId = documentId, TopicId = topicId });
        }
    }

    public async Task AddChunkAsync(int documentId, int chunkIndex, string chunkText, float[] embedding, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = """
            INSERT INTO rag.chunks (document_id, chunk_index, chunk_text, embedding)
            VALUES (@DocumentId, @ChunkIndex, @ChunkText, @Embedding);
            """;

        // Requires dataSourceBuilder.UseVector() to have been called when the
        // NpgsqlDataSource singleton was built in Program.cs, so Npgsql recognizes Pgvector.Vector.
        await connection.ExecuteAsync(query, new
        {
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            ChunkText = chunkText,
            Embedding = new Pgvector.Vector(embedding)
        });
    }

    public async Task<IEnumerable<DocumentChunk>> SearchChunksByTopicAsync(int topicId, float[] queryEmbedding, string queryText, int maxResults, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

            // RRF combines two independently-ranked candidate lists (vector similarity and keyword/FTS match)
            // into a single fused ranking, without needing to normalize distance scores against ts_rank scores.
            var query = """
        WITH vector_ranked AS (
            SELECT c.id,
                   c.embedding <=> @QueryEmbedding AS distance,
                   ROW_NUMBER() OVER (ORDER BY c.embedding <=> @QueryEmbedding) AS vec_rank
            FROM rag.chunks c
            INNER JOIN rag.document_topics dt ON dt.document_id = c.document_id
            WHERE dt.topic_id = @TopicId
            ORDER BY c.embedding <=> @QueryEmbedding
            LIMIT @CandidateFanout
        ),
        fts_ranked AS (
            SELECT c.id,
                   ROW_NUMBER() OVER (ORDER BY ts_rank_cd(c.chunk_text_tsv, plainto_tsquery('english', @QueryText)) DESC) AS fts_rank
            FROM rag.chunks c
            INNER JOIN rag.document_topics dt ON dt.document_id = c.document_id
            WHERE dt.topic_id = @TopicId
              AND c.chunk_text_tsv @@ plainto_tsquery('english', @QueryText)
            ORDER BY ts_rank_cd(c.chunk_text_tsv, plainto_tsquery('english', @QueryText)) DESC
            LIMIT @CandidateFanout
        ),
        fused AS (
            SELECT
                COALESCE(v.id, f.id) AS id,
                COALESCE(1.0 / (@RrfK + v.vec_rank), 0) + COALESCE(1.0 / (@RrfK + f.fts_rank), 0) AS fused_score,
                v.distance
            FROM vector_ranked v
            FULL OUTER JOIN fts_ranked f ON v.id = f.id
        )
        SELECT c.id, c.document_id AS DocumentId, c.chunk_index AS ChunkIndex, c.chunk_text AS ChunkText,
               fused.distance AS Distance, fused.fused_score AS FusedScore
        FROM fused
        INNER JOIN rag.chunks c ON c.id = fused.id
        ORDER BY fused.fused_score DESC
        LIMIT @MaxResults;
        """;

            var result = await connection.QueryAsync<DocumentChunk>(query, new
            {
                TopicId = topicId,
                QueryEmbedding = new Pgvector.Vector(queryEmbedding),
                QueryText = queryText,
                CandidateFanout = Math.Max(maxResults * 4, 20), // wider net feeding into fusion than final result count
                RrfK = 60, // standard RRF constant
                MaxResults = maxResults
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching chunks by topic ID {TopicId}", topicId);
            throw;
        }
    }

    public async Task<IEnumerable<Document>> GetAllDocumentsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = """
            SELECT d.id, d.title, d.original_text AS OriginalText, d.created_at AS CreatedAt,
                   COALESCE(array_agg(t.name) FILTER (WHERE t.name IS NOT NULL), ARRAY[]::text[]) AS Topics
            FROM rag.documents d
            LEFT JOIN rag.document_topics dt ON dt.document_id = d.id
            LEFT JOIN rag.topics t ON t.id = dt.topic_id
            GROUP BY d.id
            ORDER BY d.created_at DESC;
            """;

        return await connection.QueryAsync<Document>(query);
    }

    public async Task DeleteDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            // rag.chunks and rag.document_topics both cascade via FK ON DELETE CASCADE
            var query = "DELETE FROM rag.documents WHERE id = @DocumentId";
            await connection.ExecuteAsync(query, new { DocumentId = documentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document with ID {DocumentId}", documentId);
        }
    }

    public async Task UpdateDocumentAsync(int documentId, string title, string originalText, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = "UPDATE rag.documents SET title = @Title, original_text = @OriginalText WHERE id = @DocumentId";
        await connection.ExecuteAsync(query, new { DocumentId = documentId, Title = title, OriginalText = originalText });
    }

    public async Task ReplaceDocumentTopicsAsync(int documentId, IEnumerable<int> topicIds, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM rag.document_topics WHERE document_id = @DocumentId", new { DocumentId = documentId });

        var query = """
            INSERT INTO rag.document_topics (document_id, topic_id)
            VALUES (@DocumentId, @TopicId)
            ON CONFLICT DO NOTHING;
            """;

        foreach (var topicId in topicIds)
        {
            await connection.ExecuteAsync(query, new { DocumentId = documentId, TopicId = topicId });
        }
    }

    public async Task DeleteChunksByDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM rag.chunks WHERE document_id = @DocumentId", new { DocumentId = documentId });
    }

    public async Task<IEnumerable<DocumentChunk>> GetChunksByDocumentIdAsync(int documentId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = """
            SELECT id, document_id AS DocumentId, chunk_index AS ChunkIndex, chunk_text AS ChunkText
            FROM rag.chunks
            WHERE document_id = @DocumentId
            ORDER BY chunk_index;
            """;

        return await connection.QueryAsync<DocumentChunk>(query, new { DocumentId = documentId });
    }

    public async Task<IEnumerable<DocumentChunk>> GetChunksByIdsAsync(IEnumerable<int> chunkIds, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = """
            SELECT id, document_id AS DocumentId, chunk_index AS ChunkIndex, chunk_text AS ChunkText
            FROM rag.chunks
            WHERE id = ANY(@ChunkIds);
            """;

        return await connection.QueryAsync<DocumentChunk>(query, new { ChunkIds = chunkIds.ToArray() });
    }

    public async Task<(IEnumerable<ChunkListItem> Chunks, int TotalCount)> GetAllChunksAsync(int page, int pageSize, string? searchText, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var hasSearch = !string.IsNullOrWhiteSpace(searchText);
        var whereClause = hasSearch ? "WHERE c.chunk_text ILIKE @Search" : "";

        var countQuery = $"SELECT COUNT(*) FROM rag.chunks c {whereClause};";
        var totalCount = await connection.ExecuteScalarAsync<int>(countQuery, new { Search = $"%{searchText}%" });

        var query = $"""
            SELECT c.id, c.document_id AS DocumentId, d.title AS DocumentTitle,
                   c.chunk_index AS ChunkIndex, c.chunk_text AS ChunkText
            FROM rag.chunks c
            JOIN rag.documents d ON d.id = c.document_id
            {whereClause}
            ORDER BY c.document_id, c.chunk_index
            LIMIT @PageSize OFFSET @Offset;
            """;

        var chunks = await connection.QueryAsync<ChunkListItem>(query, new
        {
            Search = $"%{searchText}%",
            PageSize = pageSize,
            Offset = (page - 1) * pageSize
        });

        return (chunks, totalCount);
    }

    public async Task UpdateChunkTextAsync(int chunkId, string chunkText, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = "UPDATE rag.chunks SET chunk_text = @ChunkText WHERE id = @ChunkId";
        await connection.ExecuteAsync(query, new { ChunkId = chunkId, ChunkText = chunkText });
    }

    public async Task DeleteChunkAsync(int chunkId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = "DELETE FROM rag.chunks WHERE id = @ChunkId";
        await connection.ExecuteAsync(query, new { ChunkId = chunkId });
    }
}