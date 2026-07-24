using KnowledgeAssistant.Api.Streaming;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Domain.Conversation;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : Controller
    {
        private readonly ConversationService _conversationService;
        private readonly IConversationRepository _conversationRepository;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ConversationService conversationService, IConversationRepository conversationRepository, ILogger<ChatController> logger)
        {
            _conversationService = conversationService;
            _conversationRepository = conversationRepository;
            _logger = logger;
        }

        [HttpPost]
        public async Task Chat([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
        {
            var writer = new SseWriter(Response);
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                await writer.WriteAsync(SseEvents.Error, new ErrorEventDto
                {
                    Message = "Please select a model before sending a message."
                }, cancellationToken);

                return;
            }

            try
            {
                var conversationId = await _conversationService.EnsureConversationAsync(request, cancellationToken);
                await foreach (var token in _conversationService.GenerateAssistantMessageAsync(conversationId, request.Message, request.Model, cancellationToken))
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
                return BadRequest("Message is required.");

            if (string.IsNullOrWhiteSpace(request.Model))
                return BadRequest("Model is required.");

            var title = await _conversationService.GenerateTitleAsync(request.Message, request.Model, cancellationToken);
            return Ok(new ConversationDto { Title = title });
        }
    }
}
