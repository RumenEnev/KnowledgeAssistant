namespace KnowledgeAssistant.Application.Abstraction
{
    /// <summary>
    /// Describes a tool (function) the model may choose to call, in the shape most model providers
    /// (Ollama, OpenAI-compatible APIs) expect: a name, a human-readable description telling the model
    /// when to use it, and a JSON Schema describing its parameters.
    /// </summary>
    public class ToolDefinition
    {
        public required string Name { get; init; }

        public required string Description { get; init; }

        /// <summary>Raw JSON Schema text describing the tool's parameters object.</summary>
        public required string ParametersJsonSchema { get; init; }
    }

    /// <summary>
    /// A request from the model to invoke a tool, as returned by <see cref="IModelGateway.ChatWithToolsAsync"/>.
    /// </summary>
    public class ToolCallRequest
    {
        public required string Name { get; init; }

        /// <summary>Raw JSON object text of the arguments the model wants to call the tool with.</summary>
        public required string ArgumentsJson { get; init; }
    }

    /// <summary>
    /// The result of a single (non-streaming) tool-calling-capable chat completion: either plain text
    /// content, or one or more tool calls the caller is expected to execute and respond to.
    /// </summary>
    public class ToolChatResult
    {
        public string? Content { get; init; }

        public IReadOnlyList<ToolCallRequest> ToolCalls { get; init; } = Array.Empty<ToolCallRequest>();

        public bool HasToolCalls => ToolCalls.Count > 0;
    }
}
