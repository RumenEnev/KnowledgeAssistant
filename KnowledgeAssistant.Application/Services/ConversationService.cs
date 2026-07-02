using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Domain.Conversation;
using System.Runtime.CompilerServices;
using System.Text;

namespace KnowledgeAssistant.Application.Services
{
    public class ConversationService
    {
        private readonly IModelGateway _modelGateway;
        private readonly IConversationRepository _repository;

        public ConversationService(IModelGateway modelGateway, IConversationRepository repository)
        {
            _modelGateway = modelGateway;
            _repository = repository;
        }

        public async Task<string> GenerateTitleAsync(string userMessage, string model, CancellationToken cancellationToken)
        {
            return await _modelGateway.GenerateAsync(
                model: model,
                systemMessage: new ChatMessage
                {
                    Role = "system",
                    Content = "Generate a short, meaningful conversation title (max 6 words)"
                },
                userMessage: new ChatMessage
                {
                    Role = "user",
                    Content = userMessage
                },
                cancellationToken: cancellationToken);
        }

        public async Task<Guid> EnsureConversationAsync(ChatRequestDto request, CancellationToken cancellationToken)
        {
            if (request.ConversationId.HasValue)
            {
                var existing = await _repository.GetAsync(request.ConversationId.Value, cancellationToken);
                if (existing is not null)
                {
                    return existing.Id;
                }
            }

            var conversation = new Conversation()
            {
                Id = Guid.NewGuid(),
                Title = await GenerateTitleAsync(request.Message, request.Model ?? "llama3", cancellationToken),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(conversation, cancellationToken);
            return conversation.Id;
        }

        public async Task CreateMessageAsync(Guid conversationId, ChatMessage message, CancellationToken cancellationToken)
        {
            await _repository.CreateMessageAsync(conversationId, message, cancellationToken);
        }

        public async IAsyncEnumerable<string> SendMessageAsync(Guid? conversationId, string message, string? model, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // 1. Ensure conversation exists
            var request = new ChatRequestDto
            {
                ConversationId = conversationId,
                Message = message,
                Model = model
            };

            var resolvedConversationId = await EnsureConversationAsync(request, cancellationToken);

            // 2. Build LLM input
            var messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "user",
                    Content = message
                }
            };

            var selectedModel = model ?? "llama3";
            var buffer = new StringBuilder();

            // 3. Stream tokens from model
            await foreach (var token in _modelGateway.StreamAsync(selectedModel, messages, cancellationToken))
            {
                buffer.Append(token);
                yield return token;
            }

            // 4. Persist assistant message (optional but recommended)
            var assistantMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = resolvedConversationId,
                Role = "assistant",
                Content = buffer.ToString(),
                CreatedAt = DateTime.UtcNow
            };

            await _repository.CreateMessageAsync(resolvedConversationId, assistantMessage, cancellationToken);
        }
    }
}