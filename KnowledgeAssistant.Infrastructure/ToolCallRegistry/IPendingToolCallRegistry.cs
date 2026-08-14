namespace KnowledgeAssistant.Infrastructure.ToolCallRegistry;

public interface IPendingToolCallRegistry
{
    Task<string> WaitForResultAsync(string toolCallId, TimeSpan timeout, CancellationToken cancellationToken);

    bool TryComplete(string toolCallId, string resultJson);

    bool TryFail(string toolCallId, string errorMessage);
}