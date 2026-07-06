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

        public ChatController(ConversationService conversationService, IConversationRepository conversationRepository)
        {
            _conversationService = conversationService;
            _conversationRepository = conversationRepository;
        }

        [HttpPost]
        public async Task Chat([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
        {
            var conversationId = await _conversationService.EnsureConversationAsync(request, cancellationToken);
            var writer = new SseWriter(Response);
            await foreach (var token in _conversationService.SendMessageAsync(conversationId, request.Message, request.Model, cancellationToken))
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
    }
}
