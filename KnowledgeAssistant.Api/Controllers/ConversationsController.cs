using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Domain.Conversation;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/conversations")]
    public class ConversationsController : Controller
    {
        private readonly IConversationRepository _repository;

        public ConversationsController(IConversationRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
        {
            var conversations = await _repository.GetAllAsync(cancellationToken);
            var conversationDtos = conversations.Select(conversation => new ConversationDto
            {
                Id = conversation.Id,
                Title = conversation.Title,
                CreatedAt = conversation.CreatedAt,
            });

            return Ok(conversationDtos.OrderByDescending(c => c.CreatedAt));
        }

        [HttpPost]
        public async Task<IActionResult> CreateConversation(CancellationToken cancellationToken)
        {
            var conversation = new Conversation()
            {
                Id = Guid.NewGuid(),
                Title = "New Conversation",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await _repository.CreateAsync(conversation, cancellationToken);

            return Ok(new ConversationDto
            {
                Id = conversation.Id,
                Title = conversation.Title,
                CreatedAt = conversation.CreatedAt
            });
        }

        [HttpPatch("{conversationId}/title")]
        public async Task<IActionResult> UpdateConversationTitle(Guid conversationId, string newTitle, CancellationToken cancellationToken)
        {
            var conversation = await _repository.GetAsync(conversationId, cancellationToken);
            if (conversation == null)
            {
                return NotFound();
            }

            conversation.Title = newTitle;
            await _repository.UpdateAsync(conversation, cancellationToken);
            return Ok(new ConversationDto
            {
                Id = conversation.Id,
                Title = conversation.Title,
                CreatedAt = conversation.CreatedAt
            });
        }
    }
}