using KnowledgeAssistant.Application.Abstraction;
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
        private readonly IModelRepository _modelRepository;

        public ConversationsController(IConversationRepository repository, IModelRepository modelRepository)
        {
            _repository = repository;
            _modelRepository = modelRepository;
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
                Topic = conversation.Topic,
            });

            return Ok(conversationDtos.OrderByDescending(c => c.CreatedAt));
        }

        [HttpGet]
        [Route("{conversationId}")]
        public async Task<IActionResult> GetConversation(Guid conversationId, CancellationToken cancellationToken)
        {
            var conversation = await _repository.GetAsync(conversationId, cancellationToken);
            if (conversation == null)
            {
                return NotFound();
            }

            string? selectedModel = null;
            if (conversation.SelectedModelId.HasValue && conversation.SelectedModelId != Guid.Empty)
            {
                selectedModel = await _modelRepository.GetModelNameAsync(conversation.SelectedModelId.Value, cancellationToken);
            }

            var conversationDto = new ConversationDto
            {
                Id = conversation.Id,
                Title = conversation.Title,
                CreatedAt = conversation.CreatedAt,
                SelectedModel = selectedModel,
                Topic = conversation.Topic,
                Messages = conversation.Messages?.Select(message => new MessageDto
                {
                    Id = message.Id,
                    Role = message.Role,
                    Content = message.Content,
                    CreatedAt = message.CreatedAt
                })
            };

            return Ok(conversationDto);
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

        [HttpDelete("{conversationId}")]
        public async Task<IActionResult> DeleteConversation(Guid conversationId, CancellationToken cancellationToken)
        {
            var conversation = await _repository.GetAsync(conversationId, cancellationToken);
            if (conversation == null)
            {
                return NotFound();
            }

            var deletedConversationId = await _repository.DeleteConversationAsync(conversationId, cancellationToken);
            if (deletedConversationId == Guid.Empty)
            {
                return StatusCode(500, "An error occurred while deleting the conversation.");
            }

            return Ok(deletedConversationId);
        }
    }
}