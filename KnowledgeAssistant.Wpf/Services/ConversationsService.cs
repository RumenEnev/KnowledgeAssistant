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

                    _messageService.Publish(new SendUserPromptRequest(request.Prompt, request.Model, null));
                }
            }
        }
    }
}