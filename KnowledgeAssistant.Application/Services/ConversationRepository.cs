using Dapper;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain.Conversation;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KnowledgeAssistant.Application.Services
{
    public class ConversationRepository : IConversationRepository
    {
        private readonly string _connectionString;

        public ConversationRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("KnowledgeAssistant")
                ?? throw new InvalidOperationException("Connection string is missing.");
        }

        public async Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken cancellationToken)
        {
            await using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT c.id, c.title, c.created_at AS CreatedAt, c.updated_at AS UpdatedAt, " +
                    "c.topic_id AS TopicId, t.name AS Topic " +
                    "FROM ai_interactions.conversations c " +
                    "LEFT JOIN rag.topics t ON t.id = c.topic_id";
                return await connection.QueryAsync<Conversation>(query);
            }
        }

        public async Task CreateAsync(Conversation conversation, CancellationToken cancellationToken)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                var query = $"INSERT INTO ai_interactions.conversations (id, title, created_at, updated_at, selected_model_id) VALUES " +
                    $"(@Id, @Title, @CreatedAt, @UpdatedAt, @SelectedModelId)";
                
                await connection.ExecuteAsync(query, new
                {
                    Id = conversation.Id,
                    Title = conversation.Title,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SelectedModelId = conversation.SelectedModelId ?? Guid.Empty,
                });
            }
        }

        public async Task CreateMessageAsync(Guid conversationId, ChatMessage message, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var query = "INSERT INTO ai_interactions.chat_messages (id, conversation_id, role, content, created_at, tokens_count) VALUES " +
                        "(@Id, @ConversationId, @Role, @Content, @CreatedAt, @TokensCount)";

            await connection.ExecuteAsync(query, new
            {
                Id = message.Id,
                ConversationId = conversationId,
                Role = message.Role,
                Content = message.Content,
                CreatedAt = DateTime.UtcNow,
                TokensCount = message.TokensCount
            });
        }

        public async Task<Conversation?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var query = "SELECT c.id, c.title, c.created_at AS CreatedAt, c.updated_at AS UpdatedAt, c.selected_model_id AS SelectedModelId, " +
                "c.topic_id AS TopicId, t.name AS Topic " +
                "FROM ai_interactions.conversations c " +
                "LEFT JOIN rag.topics t ON t.id = c.topic_id " +
                "WHERE c.id = @Id";
            var conversation = await connection.QuerySingleOrDefaultAsync<Conversation>(query, new { Id = id });
            if (conversation != null)
            {
                var messagesQuery = "SELECT * FROM ai_interactions.chat_messages WHERE conversation_id = @ConversationId ORDER BY created_at";
                var messages = await connection.QueryAsync<ChatMessage>(messagesQuery, new { ConversationId = id });
                conversation.Messages = messages;
            }

            return conversation;
        }

        public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var query = "UPDATE ai_interactions.conversations SET " +
                "title = @Title, " +
                "updated_at = @UpdatedAt " +
                "WHERE id = @Id";

            await connection.ExecuteAsync(query, new
            {
                Id = conversation.Id,
                Title = conversation.Title,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        public async Task<Guid> DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                var deleteMessagesQuery = "DELETE FROM ai_interactions.chat_messages WHERE conversation_id = @ConversationId";
                await connection.ExecuteAsync(deleteMessagesQuery, new { ConversationId = conversationId });

                var deleteConversationQuery = "DELETE FROM ai_interactions.conversations WHERE id = @Id";
                await connection.ExecuteAsync(deleteConversationQuery, new { Id = conversationId });
            }
            catch 
            {
                return Guid.Empty;
            }

            return conversationId;
        }

        public async Task<ChatMessage?> GetLastAssistantMessageAsync(Guid conversationId, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var query = "SELECT * FROM ai_interactions.chat_messages WHERE conversation_id = @ConversationId AND role = 'assistant' ORDER BY created_at DESC LIMIT 1";
            var message = await connection.QuerySingleOrDefaultAsync<ChatMessage>(query, new { ConversationId = conversationId });
            return message;
        }

        public async Task UpdateSelectedModelAsync(Guid conversationId, Guid modelId, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var query = "UPDATE ai_interactions.conversations SET selected_model_id = @ModelId, updated_at = @UpdatedAt WHERE id = @Id";
            await connection.ExecuteAsync(query, new
            {
                Id = conversationId,
                ModelId = modelId,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public async Task UpdateTopicAsync(Guid conversationId, int? topicId, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var query = "UPDATE ai_interactions.conversations SET topic_id = @TopicId, updated_at = @UpdatedAt WHERE id = @Id";
            await connection.ExecuteAsync(query, new
            {
                Id = conversationId,
                TopicId = topicId,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}