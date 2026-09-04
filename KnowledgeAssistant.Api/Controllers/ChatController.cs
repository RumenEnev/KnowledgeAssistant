using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Contracts.Dto.Conversation;
using KnowledgeAssistant.Infrastructure.Streaming;
using KnowledgeAssistant.Infrastructure.ToolCallRegistry;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace KnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : Controller
{
    private readonly ConversationService _conversationService;
    private readonly IConversationRepository _conversationRepository;
    private readonly SseWriterAccessor _sseWriterAccessor;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ConversationService conversationService,
        IConversationRepository conversationRepository,
        SseWriterAccessor sseWriterAccessor,
        ILogger<ChatController> logger)
    {
        _conversationService = conversationService;
        _conversationRepository = conversationRepository;
        _sseWriterAccessor = sseWriterAccessor;
        _logger = logger;
    }

    [HttpPost]
    public async Task Chat([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        var writer = new SseWriter(Response);
        _sseWriterAccessor.Writer = writer;
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            await writer.WriteAsync(SseEvents.Error, new ErrorEventDto
            {
                Message = "Please select a model before sending a message."
            }, cancellationToken);

            return;
        }

        if (!request.ConversationId.HasValue && string.IsNullOrWhiteSpace(request.Provider))
        {
            await writer.WriteAsync(SseEvents.Error, new ErrorEventDto
            {
                Message = "Please select a model provider before sending a message."
            }, cancellationToken);

            return;
        }

        try
        {
            _logger.LogInformation("Received chat message for conversation {ConversationId} from {Source} client.", request.ConversationId, request.Source);
            await writer.WriteAsync(SseEvents.Progress, new ProgressEventDto { Message = "Analyzing your request..." }, cancellationToken);
            var conversationId = await _conversationService.EnsureConversationAsync(request, cancellationToken);
            await writer.WriteAsync(SseEvents.Progress, new ProgressEventDto { Message = "Retrieving relevant context and generating a response..." }, cancellationToken);
            await foreach (var token in _conversationService.GenerateAssistantMessageAsync(conversationId, request.Message, request.Model, request.Source, cancellationToken))
            {
                await writer.WriteAsync(SseEvents.Token, new { conversationId, content = token }, cancellationToken);
            }

            var lastAssistantMessage = await _conversationRepository.GetLastAssistantMessageAsync(conversationId, cancellationToken);
            var (promptTokens, responseTokens) = _conversationService.GetTokenConsumption();
            await writer.WriteAsync(SseEvents.MessageCompleted, new ChatResponseChunkDto()
            {
                ConversationId = conversationId,
                MessageId = lastAssistantMessage?.Id
            }, cancellationToken);

            await writer.WriteAsync(SseEvents.Done, new MessageDoneDto
            {
                PromptTokens = promptTokens,
                ResponseTokens = responseTokens
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected or cancelled the request; nothing to notify.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while streaming chat response.");

            await writer.WriteAsync(SseEvents.Error, new ErrorEventDto
            {
                Message = "Something went wrong while processing your message. Please try again."
            }, cancellationToken);
        }
    }

    [HttpPost("title")]
    public async Task<IActionResult> GenerateTitle([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest("Message is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return BadRequest("Model is required.");
        }

        var title = await _conversationService.GenerateTitleAsync((Guid)request.ConversationId, request.Message, request.Model, cancellationToken);
        return Ok(new ConversationDto { Title = title });
    }

    [HttpPost("tool-calls/{toolCallId}/result")]
    public IActionResult SubmitToolCallResult(string toolCallId, [FromBody] ToolResult result, [FromServices] IPendingToolCallRegistry registry)
    {
        var resultJson = JsonSerializer.Serialize(result);
        if (!registry.TryComplete(toolCallId, resultJson))
        {
            return NotFound($"No pending tool call found for '{toolCallId}' (already completed, timed out, or never issued).");
        }

        return Accepted();
    }

    [HttpPost("tool-calls/{toolCallId}/intermediate")]
    public IActionResult SubmitToolCallIntermediate(string toolCallId, [FromServices] IPendingToolCallRegistry registry)
    {
        if (!registry.ResetTimer(toolCallId))
        {
            return NotFound($"No pending tool call found for '{toolCallId}' (already completed, timed out, or never issued).");
        }

        return Accepted();
    }
}