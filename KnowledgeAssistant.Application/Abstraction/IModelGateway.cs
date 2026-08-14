using KnowledgeAssistant.Domain.Conversation;

namespace KnowledgeAssistant.Application.Abstraction;

public interface IModelGateway
{
    IAsyncEnumerable<string> StreamAsync(string model, List<ChatMessage> messages, CancellationToken cancellationToken);
    
    Task<string> GenerateAsync(string model, ChatMessage userMessage, ChatMessage systemMessage, CancellationToken cancellationToken);

    (int, int) GetTokenConsumption();

    Task<float[]> GetEmbeddingAsync(string model, string text, CancellationToken cancellationToken);

    Task<ToolChatResult> ChatWithToolsAsync(string model, ChatMessage userMessage, ChatMessage systemMessage, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken);
}