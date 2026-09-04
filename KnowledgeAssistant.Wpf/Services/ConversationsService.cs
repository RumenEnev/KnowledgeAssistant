using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Wpf.Messages;
using KnowledgeAssistant.Wpf.Messages.Conversations;
using KnowledgeAssistant.Wpf.Messages.ModelsManagement;
using KnowledgeAssistant.Wpf.Models;
using MessageServices;
using MessageServices.Enums;
using MessageServices.Messages;

namespace KnowledgeAssistant.Wpf.Services
{
    public class ConversationsService : IMessageServiceSubscriber
    {
        private const string DefaultConversationTitle = "New Conversation";

        private readonly MessageService _messageService;
        private readonly HashSet<Guid> _titleGenerationRequested = new();
        private ConversationCompositionModel? _conversation;
        private string _currentModel = string.Empty;

        public ConversationsService(MessageService messageService)
        {
            _messageService = messageService;

            _messageService.Subscribe<SendUserMessageRequest>(this, SendUserMessageReceived);
            _messageService.Subscribe<SendToolPromptRequest>(this, SendToolPromptReceived);
            _messageService.Subscribe<TitleGeneratedEvent>(this, TitleGeneratedReceived);
            _messageService.Subscribe<SelectedConversationChangedRequest>(this, SelectedConversationChangedReceived);
            _messageService.Subscribe<ConversationLoadedEvent>(this, ConversationLoadedReceived);
            _messageService.Subscribe<ConversationDeletedEvent>(this, ConversationDeletedEventReceived);
            _messageService.Subscribe<CreateConversationsRequest>(this, CreateConversationsReceived);
            _messageService.Subscribe<UpdateConversationModelSelectionRequest>(this, UpdateSelectedModelReceived);
        }

        private async void SendUserMessageReceived(MessageBase message)
        {
            if (message is SendUserMessageRequest request)
            {
                if (_conversation == null)
                {
                    await CreateNewConversationAsync(request.Provider, request.Model);
                }

                if (_conversation != null && ShouldGenerateTitle(_conversation))
                {
                    _titleGenerationRequested.Add(_conversation.Id);
                    _messageService.Publish(new GenerateTitleRequest(request.Prompt, _conversation.SelectedModel ?? _currentModel, _conversation.Id));
                }

                _messageService.Publish(new SendPromptRequest(request.Prompt, _conversation?.SelectedModel ?? _currentModel, "user", _conversation?.Id));
            }
        }

        private void SendToolPromptReceived(MessageBase message)
        {
            if (message is SendToolPromptRequest request)
            {
                if (_conversation == null)
                {
                    _messageService.Publish(new UserMessage("Error", "No active conversation to send the tool prompt.", MessageType.Error));
                    return;
                }

                _messageService.Publish(new SendPromptRequest(request.Context, _conversation.SelectedModel ?? _currentModel, "tool", _conversation.Id, request.SystemPrompt));
            }
        }

        private void UpdateSelectedModelReceived(MessageBase message)
        {
            if (message is UpdateConversationModelSelectionRequest request)
            {
                _currentModel = request.SelectedModel;
                if (_conversation != null)
                {
                    _conversation.SelectedModel = request.SelectedModel;
                }
            }
        }

        private async void CreateConversationsReceived(MessageBase message)
        {
            if (message is CreateConversationsRequest request)
            {
                await CreateNewConversationAsync(request.Provider, request.Model);
                _messageService.Publish(new ConversationCreatedEvent(new Conversation()
                {
                    Id = _conversation?.Id ?? Guid.Empty,
                    Title = _conversation?.Title ?? string.Empty,
                    CreatedAt = _conversation?.CreatedOn ?? DateTime.UtcNow,
                    UpdatedAt = _conversation?.UpdatedOn ?? DateTime.UtcNow,
                    Provider = ModelProviderNames.Unknown
                }));
            }
        }

        private void ConversationDeletedEventReceived(MessageBase message)
        {
            if (message is ConversationDeletedEvent @event)
            {
                _titleGenerationRequested.Remove(@event.ConversationId);
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

        private bool ShouldGenerateTitle(ConversationCompositionModel conversation)
        {
            return !_titleGenerationRequested.Contains(conversation.Id) &&
                (string.IsNullOrWhiteSpace(conversation.Title) || conversation.Title == DefaultConversationTitle);
        }

        private async Task CreateNewConversationAsync(string provider, string model)
        {
            var result = await _messageService.RequestAsync<CreateConversationsResponse>(new CreateConversationsRequest(provider, model));
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