using System.Collections.Concurrent;

namespace KnowledgeAssistant.Infrastructure.ToolCallRegistry;

public sealed class PendingToolCallRegistry : IPendingToolCallRegistry
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();

    public async Task<string> WaitForResultAsync(string toolCallId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pending.TryAdd(toolCallId, tcs))
            throw new InvalidOperationException($"A pending call already exists for toolCallId '{toolCallId}'.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        await using var registration = timeoutCts.Token.Register(() =>
            tcs.TrySetException(new TimeoutException(
                $"Timed out waiting for client to execute tool call '{toolCallId}'.")));

        try
        {
            return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(toolCallId, out _);
        }
    }

    public bool TryComplete(string toolCallId, string resultJson) =>
        _pending.TryGetValue(toolCallId, out var tcs) && tcs.TrySetResult(resultJson);

    public bool TryFail(string toolCallId, string errorMessage) =>
        _pending.TryGetValue(toolCallId, out var tcs) &&
        tcs.TrySetException(new InvalidOperationException(errorMessage));
}