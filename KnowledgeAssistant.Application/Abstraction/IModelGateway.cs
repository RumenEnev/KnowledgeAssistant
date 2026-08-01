using KnowledgeAssistant.Domain.Conversation;

namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IModelGateway
    {
        IAsyncEnumerable<string> StreamAsync(string model, List<ChatMessage> messages, CancellationToken cancellationToken);
        
        Task<string> GenerateAsync(string model, ChatMessage userMessage, ChatMessage systemMessage, CancellationToken cancellationToken);
    
        (int, int) GetTokenConsumption();

        Task<float[]> GetEmbeddingAsync(string model, string text, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a single-turn chat request (system + user message) along with a list of tools the model
        /// may call. Returns either plain text content or the tool call(s) the model wants to make.
        /// Models/providers that don't support tool calling will simply return plain content with no tool calls.
        /// </summary>
        Task<ToolChatResult> ChatWithToolsAsync(string model, ChatMessage userMessage, ChatMessage systemMessage, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken);
    }
}