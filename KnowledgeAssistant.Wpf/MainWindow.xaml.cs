using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Wpf.Messages;
using KnowledgeAssistant.Wpf.Messages.Conversations;
using KnowledgeAssistant.Wpf.UserControls;
using MessageServices;
using MessageServices.Enums;
using MessageServices.Messages;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace KnowledgeAssistant.Wpf
{
    public partial class MainWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;
        private string? _selectedModel;
        private string? _userPrompt;
        private Thickness _chatMessageMargin;
        private Conversation? _selectedConversation;
        private Guid? _lastConversationId;
        private ObservableCollection<string>? _models;
        private ObservableCollection<Conversation> _conversations;
        private ObservableCollection<UCChatMessage> _chatMessages = new ObservableCollection<UCChatMessage>();

        public MainWindow(MessageService messageService)
        {
            InitializeComponent();
            DataContext = this;

            _conversations = new ObservableCollection<Conversation>();

            _messageService = messageService;
            _messageService.Subscribe<AvailableModelsUpdatedEvent>(this, AvailableModelsUpdatedEventReceived);
            _messageService.Subscribe<ChatCompletedEvent>(this, ChatCompletedEventReceived);
            _messageService.Subscribe<TitleGeneratedEvent>(this, TitleGeneratedEventReceived);
            _messageService.Subscribe<ConversationsUpdatedEvent>(this, ConversationsUpdatedEventReceived);
            _messageService.Subscribe<UserMessage>(this, UserMessageReceived);
        }

        public string? SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (_selectedModel != value)
                {
                    _selectedModel = value;
                    OnPropertyChanged(nameof(SelectedModel));
                }
            }
        }

        public string? UserPrompt
        {
            get => _userPrompt;
            set
            {
                if (_userPrompt != value)
                {
                    _userPrompt = value;
                    OnPropertyChanged(nameof(UserPrompt));
                }
            }
        }

        public Thickness ChatMessageMargin
        {
            get => _chatMessageMargin;
            set
            {
                _chatMessageMargin = value;
                OnPropertyChanged(nameof(ChatMessageMargin));
            }
        }

        public Conversation? SelectedConversation
        {
            get => _selectedConversation;
            set
            {
                if (_selectedConversation != value)
                {
                    _selectedConversation = value;
                    OnPropertyChanged(nameof(SelectedConversation));

                    if (_selectedConversation != null)
                    {
                        _messageService.Publish(new SelectedConversationChangedRequest(_selectedConversation.Id));
                    }
                }
            }
        }

        public ObservableCollection<string>? Models
        {
            get => _models;
            set
            {
                if (_models != value)
                {
                    _models = value;
                    OnPropertyChanged(nameof(Models));
                }
            }
        }

        public ObservableCollection<Conversation> Conversations
        {
            get => _conversations;
            set
            {
                if (_conversations != value)
                {
                    _conversations = value;
                    OnPropertyChanged(nameof(Conversations));
                }
            }
        }

        public ObservableCollection<UCChatMessage> ChatMessages
        {
            get => _chatMessages;
            set
            {
                if (_chatMessages != value)
                {
                    _chatMessages = value;
                    OnPropertyChanged(nameof(ChatMessages));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UserMessageReceived(MessageBase message)
        {
            if (message is UserMessage userMessage)
            {
                MessageBox.Show(userMessage.Message, userMessage.Title, MessageBoxButton.OK,
                    userMessage.MessageType == MessageType.Error ? MessageBoxImage.Error : MessageBoxImage.Information);
            }
        }

        private void ConversationsUpdatedEventReceived(MessageBase message)
        {
            if (message is ConversationsUpdatedEvent conversationsUpdatedEvent)
            {
                Conversations = new ObservableCollection<Conversation>(conversationsUpdatedEvent.Conversations);
                SelectedConversation = _lastConversationId.HasValue
                    ? Conversations.FirstOrDefault(c => c.Id == _lastConversationId) ?? Conversations.FirstOrDefault()
                    : Conversations.FirstOrDefault();
            }
        }

        private void AvailableModelsUpdatedEventReceived(MessageBase message)
        {
            if (message is AvailableModelsUpdatedEvent availableModelsUpdatedEvent)
            {
                Models = new ObservableCollection<string>(availableModelsUpdatedEvent.Models);
                SelectedModel = Models.FirstOrDefault();
            }
        }

        private void ChatCompletedEventReceived(MessageBase message)
        {
            if (message is ChatCompletedEvent completedEvent)
            {
                ChatMessages.Last().MessageCompleted = true;
                _lastConversationId = completedEvent.ConversationId;
                _messageService.Publish(new GetConversationsRequest());
            }
        }

        private void TitleGeneratedEventReceived(MessageBase message)
        {
            if (message is TitleGeneratedEvent request)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var conversationToUpdate = Conversations.FirstOrDefault(c => c.Id == request.ConversationId);
                    if (conversationToUpdate != null)
                    {
                        conversationToUpdate.Title = request.Title;
                        OnPropertyChanged(nameof(Conversations));
                        SelectedConversation = conversationToUpdate;
                    }
                    else
                    {
                        var conversation = new Conversation
                        {
                            Id = request.ConversationId,
                            Title = request.Title,
                        };

                        Conversations.Add(conversation);
                        SelectedConversation = conversation;
                    }
                });
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new GetAvailableModelsRequest());
            _messageService.Publish(new GetConversationsRequest());
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(UserPrompt) && !string.IsNullOrWhiteSpace(SelectedModel))
            {
                var userMessage = new UCChatMessage(_messageService)
                {
                    Message = UserPrompt,
                    MessageCompleted = true,
                    IsUserMessage = true
                };

                ChatMessages.Add(userMessage);
                ChatMessages.Add(new UCChatMessage(_messageService));
                _messageService.Publish(new SendUserMessageRequest(UserPrompt, SelectedModel, SelectedConversation?.Id));
                UserPrompt = string.Empty;
            }
        }

        private void CreateConversation_Click(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new CreateConversationsRequest());
        }

        private void RenameConversation_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}