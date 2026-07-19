using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Wpf.Messages;
using KnowledgeAssistant.Wpf.Messages.Conversations;
using KnowledgeAssistant.Wpf.Models;
using MessageServices;
using MessageServices.Enums;
using MessageServices.Messages;

namespace KnowledgeAssistant.Wpf.Services
{
    public class ConversationsService : IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;
        private ConversationCompositionModel? _conversation;

        public ConversationsService(MessageService messageService)
        {
            _messageService = messageService;

            _messageService.Subscribe<SendUserMessageRequest>(this, SendUserMessageReceived);
            _messageService.Subscribe<TitleGeneratedEvent>(this, TitleGeneratedReceived);
            _messageService.Subscribe<SelectedConversationChangedRequest>(this, SelectedConversationChangedReceived);
            _messageService.Subscribe<ConversationLoadedEvent>(this, ConversationLoadedReceived);
            _messageService.Subscribe<ConversationDeletedEvent>(this, ConversationDeletedEventReceived);
            _messageService.Subscribe<CreateConversationsRequest>(this, CreateConversationsReceived);

        }

        private async void CreateConversationsReceived(MessageBase message)
        {
            if (message is CreateConversationsRequest)
            {
                await CreateNewConversationAsync();
                _messageService.Publish(new ConversationCreatedEvent(new Conversation()
                {
                    Id = _conversation?.Id ?? Guid.Empty,
                    Title = _conversation?.Title ?? string.Empty,
                    CreatedAt = _conversation?.CreatedOn ?? DateTime.UtcNow,
                    UpdatedAt = _conversation?.UpdatedOn ?? DateTime.UtcNow
                }));
            }
        }

        private void ConversationDeletedEventReceived(MessageBase message)
        {
            if (message is ConversationDeletedEvent @event)
            {
                if (_conversation != null && _conversation.Id == @event.ConversationId)
                {
                    _conversation = null;
                }
            }
        }

        private void ConversationLoadedReceived(MessageBase message)
        {
            if (message is ConversationLoadedEvent @event)
            {
                var dto = @event.Dto;
                _conversation = new ConversationCompositionModel
                {
                    Id = dto.Id,
                    Title = dto.Title,
                    CreatedOn = dto.CreatedAt,
                    SelectedModel = dto.SelectedModel,
                    Messages = dto.Messages?.Select(m => new ChatMessage
                    {
                        Id = m.Id,
                        Role = m.Role,
                        Content = m.Content,
                        CreatedAt = m.CreatedAt
                    })
                };

                _messageService.Publish(new UpdateConversationMessages(_conversation));
            }
        }

        private void SelectedConversationChangedReceived(MessageBase message)
        {
            if (message is SelectedConversationChangedRequest request)
            {
                _messageService.Publish(new GetConversationRequest(request.ConversationId));
            }
        }

        private void TitleGeneratedReceived(MessageBase message)
        {
            if (message is TitleGeneratedEvent @event)
            {
                if (_conversation != null)
                {
                    _conversation.Title = @event.Title;
                    _conversation.UpdatedOn = DateTime.UtcNow;
                }
            }
        }

        private async void SendUserMessageReceived(MessageBase message)
        {
            if (message is SendUserMessageRequest request)
            {
                if (_conversation == null)
                {
                    await CreateNewConversationAsync();
                    _messageService.Publish(new SendUserPromptRequest(request.Prompt, request.Model, null));
                }
            }
        }

        private async Task CreateNewConversationAsync()
        {
            var result = await _messageService.RequestAsync<CreateConversationsResponse>(new CreateConversationsRequest());
            var conversation = result.FirstOrDefault()?.Conversation;
            if (conversation == null || conversation.Id == Guid.Empty)
            {
                _messageService.Publish(new UserMessage("Error", "Failed to create a new conversation.", MessageType.Error));
                return;
            }

            _conversation = new ConversationCompositionModel
            {
                Id = conversation.Id,
                Title = conversation.Title,
                CreatedOn = conversation.CreatedAt,
                UpdatedOn = conversation.CreatedAt
            };
        }
    }
}