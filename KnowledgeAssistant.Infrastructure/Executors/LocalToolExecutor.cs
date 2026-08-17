using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Domain;
using KnowledgeAssistant.Infrastructure.Streaming;
using KnowledgeAssistant.Infrastructure.ToolCallRegistry;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public sealed class LocalToolExecutor : IToolExecutor
{
    private readonly SseWriterAccessor _sseWriterAccessor;
    private readonly IPendingToolCallRegistry _pendingCalls;
    private readonly ILogger<LocalToolExecutor> _logger;

    private static readonly TimeSpan ClientToolTimeout = TimeSpan.FromMinutes(3);

    public LocalToolExecutor(
        SseWriterAccessor sseWriterAccessor,
        IPendingToolCallRegistry pendingCalls,
        ILogger<LocalToolExecutor> logger)
    {
        _sseWriterAccessor = sseWriterAccessor;
        _pendingCalls = pendingCalls;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(ToolDefinitionEntity tool, string argumentsJson, CancellationToken cancellationToken)
    {
        var writer = _sseWriterAccessor.Writer ?? throw new InvalidOperationException("No SseWriter is available on this request. LocalToolExecutor can only run inside a streaming request.");
        var toolCallId = Guid.NewGuid().ToString();
        _logger.LogInformation("Handing off tool '{ToolName}' (call {ToolCallId}) to client for execution.", tool.Name, toolCallId);
        await writer.WriteAsync(
            SseEvents.ToolCall,
            new ToolCallDto
            {
                ToolCallId = Guid.Parse(toolCallId),
                ToolId = tool.Id,
                ToolName = tool.Name,
                ToolPath = tool.Path,
                Arguments = JsonSerializer.Deserialize<JsonElement>(argumentsJson)
            },
            cancellationToken);

        try
        {
            return await _pendingCalls.WaitForResultAsync(toolCallId, ClientToolTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Tool call {ToolCallId} timed out waiting for client execution.", toolCallId);
            await writer.WriteAsync(
                SseEvents.Error,
                new { toolCallId, message = $"Client did not report a result for '{tool.Name}' in time." },
                cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "error",
                reason = "client_timeout",
                message = $"Client did not report a result for '{tool.Name}' within {ClientToolTimeout}."
            });
        }
    }
}