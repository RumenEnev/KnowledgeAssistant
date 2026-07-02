using KnowledgeAssistant.Api.Streaming;
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

        public ChatController(ConversationService conversationService)
        {
            _conversationService = conversationService;
        }

        [HttpPost]
        public async Task Chat([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
        {
            var writer = new SseWriter(Response);
            var conversationId = await _conversationService.EnsureConversationAsync(request, cancellationToken);
            await _conversationService.CreateMessageAsync(conversationId, new ChatMessage()
            {
                Id = Guid.NewGuid(),
                Content = request.Message,
                ConversationId = conversationId,
                Role = "user",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await writer.WriteAsync(SseEvents.ConversationUpdated, new { conversationId }, cancellationToken);
            await foreach (var token in _conversationService.SendMessageAsync(conversationId, request.Message, request.Model, cancellationToken))
            {
                await writer.WriteAsync(SseEvents.Token, new { conversationId, content = token }, cancellationToken);
            }

            await writer.WriteAsync(SseEvents.MessageCompleted, new { conversationId }, cancellationToken);
            await writer.WriteAsync(SseEvents.Done, new { }, cancellationToken);
        }
    }
}
