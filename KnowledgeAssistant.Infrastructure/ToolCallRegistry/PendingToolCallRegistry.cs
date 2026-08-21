using System.Collections.Concurrent;

namespace KnowledgeAssistant.Infrastructure.ToolCallRegistry;

public sealed class PendingToolCallRegistry : IPendingToolCallRegistry
{
    private readonly ConcurrentDictionary<string, PendingCall> _pending = new();

    private sealed class PendingCall
    {
        public required TaskCompletionSource<string> Tcs { get; init; }
        public required CancellationTokenSource TimeoutCts { get; init; }
        public required TimeSpan Timeout { get; init; }
    }

    public async Task<string> WaitForResultAsync(string toolCallId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pendingCall = new PendingCall
        {
            Tcs = tcs,
            TimeoutCts = timeoutCts,
            Timeout = timeout
        };

        if (!_pending.TryAdd(toolCallId, pendingCall))
        {
            timeoutCts.Dispose();
            throw new InvalidOperationException($"A pending call already exists for toolCallId '{toolCallId}'.");
        }

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
            timeoutCts.Dispose();
        }
    }

    public bool TryComplete(string toolCallId, string resultJson) =>
        _pending.TryGetValue(toolCallId, out var pendingCall) && pendingCall.Tcs.TrySetResult(resultJson);

    public bool TryFail(string toolCallId, string errorMessage) =>
        _pending.TryGetValue(toolCallId, out var pendingCall) &&
        pendingCall.Tcs.TrySetException(new InvalidOperationException(errorMessage));

    public bool ResetTimer(string toolCallId)
    {
        if (!_pending.TryGetValue(toolCallId, out var pendingCall))
        {
            return false;
        }

        try
        {
            pendingCall.TimeoutCts.CancelAfter(pendingCall.Timeout);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}