using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Wpf.Messages;
using KnowledgeAssistant.Wpf.Messages.Conversations;
using KnowledgeAssistant.Wpf.UserControls;
using KnowledgeAssistant.Wpf.Windows;
using MessageServices;
using MessageServices.Enums;
using MessageServices.Messages;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UI.Windows;

namespace KnowledgeAssistant.Wpf
{
    public partial class MainWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;
        private string? _selectedModel;
        private string? _userPrompt;
        private string? _statusMessage;
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
            _chatMessages.CollectionChanged += ChatMessages_CollectionChanged;

            _messageService = messageService;
            _messageService.Subscribe<UserMessage>(this, UserMessageReceived);
            _messageService.Subscribe<AvailableModelsUpdatedEvent>(this, AvailableModelsUpdatedEventReceived);
            _messageService.Subscribe<ChatCompletedEvent>(this, ChatCompletedEventReceived);
            _messageService.Subscribe<TitleGeneratedEvent>(this, TitleGeneratedEventReceived);
            _messageService.Subscribe<ConversationsUpdatedEvent>(this, ConversationsUpdatedEventReceived);
            _messageService.Subscribe<UpdateConversationMessages>(this, UpdateConversationMessagesReceived);
            _messageService.Subscribe<ConversationCreatedEvent>(this, ConversationCreatedEventReceived);
            _messageService.Subscribe<ConversationUpdatedEvent>(this, ConversationUpdatedEventReceived);
            _messageService.Subscribe<ConversationDeletedEvent>(this, ConversationDeletedEventReceived);
            _messageService.Subscribe<SelectedModelUpdatedEvent>(this, SelectedModelUpdatedEventReceived);
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

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        _messageService.Publish(new UpdateSelectedModelRequest(value));
                    }
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

        public string? StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
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
                _conversations = value;
                OnPropertyChanged(nameof(Conversations));
            }
        }

        public ObservableCollection<UCChatMessage> ChatMessages
        {
            get => _chatMessages;
            set
            {
                if (_chatMessages != value)
                {
                    _chatMessages.CollectionChanged -= ChatMessages_CollectionChanged;
                    _chatMessages = value;
                    _chatMessages.CollectionChanged += ChatMessages_CollectionChanged;
                    OnPropertyChanged(nameof(ChatMessages));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ScrollViewer? FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var result = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
                if (result != null) return result;
            }
            return null;
        }

        private void ChatMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var scrollViewer = FindScrollViewer(ChatListView);
                        scrollViewer?.ScrollToBottom();
                    }), DispatcherPriority.Background);
                }), DispatcherPriority.Background);
            }
        }

        private void ConversationCreatedEventReceived(MessageBase message)
        {
            if (message is ConversationCreatedEvent request)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Conversations.Insert(0, request.Conversation);
                    SelectedConversation = request.Conversation;
                });
            }
        }

        private void ConversationDeletedEventReceived(MessageBase message)
        {
            if (message is ConversationDeletedEvent deleteEvent)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var conversationToDelete = Conversations.FirstOrDefault(c => c.Id == deleteEvent.ConversationId);
                    if (conversationToDelete != null)
                    {
                        Conversations.Remove(conversationToDelete);
                        if (Conversations.Any())
                        {
                            SelectedConversation = Conversations.FirstOrDefault();
                        }
                    }
                });
            }
        }

        private void ConversationUpdatedEventReceived(MessageBase message)
        {
            if (message is ConversationUpdatedEvent request)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var conversationToUpdate = Conversations.FirstOrDefault(c => c.Id == request.Conversation.Id);
                    if (conversationToUpdate != null)
                    {
                        conversationToUpdate.Title = request.Conversation.Title;
                        conversationToUpdate.Topic = request.Conversation.Topic;
                        Conversations = new ObservableCollection<Conversation>(Conversations);
                        SelectedConversation = conversationToUpdate;
                    }
                });
            }
        }

        private void UpdateConversationMessagesReceived(MessageBase message)
        {
            Application.Current.Dispatcher.Invoke(ChatMessages.Clear);
            if (message is UpdateConversationMessages request)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(request.Conversation?.SelectedModel) && Models != null && Models.Contains(request.Conversation.SelectedModel))
                    {
                        SelectedModel = request.Conversation.SelectedModel;
                    }

                    if (request.Conversation?.Messages?.Any() == true)
                    {
                        foreach (var msg in request.Conversation.Messages)
                        {
                            var ucMessage = new UCChatMessage(null)
                            {
                                Message = msg.Content,
                                IsUserMessage = msg.Role == "user",
                                MessageCompleted = true
                            };

                            ChatMessages.Add(ucMessage);
                        }
                    }
                });
            }
        }

        private void UserMessageReceived(MessageBase message)
        {
            if (message is UserMessage userMessage)
            {
                if (userMessage.MessageType == MessageType.ShortInfo)
                {
                    StatusMessage = userMessage.Message;
                }
                else
                {
                    MessageBox.Show(userMessage.Message, userMessage.Title, MessageBoxButton.OK,
                        userMessage.MessageType == MessageType.Error ? MessageBoxImage.Error : MessageBoxImage.Information);
                }
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
                _messageService.Publish(new GetSelectedModelRequest());
            }
        }

        private void SelectedModelUpdatedEventReceived(MessageBase message)
        {
            if (message is SelectedModelUpdatedEvent request)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(request.SelectedModel) && Models != null && Models.Contains(request.SelectedModel))
                    {
                        SelectedModel = request.SelectedModel;
                    }
                });
            }
        }

        private void ChatCompletedEventReceived(MessageBase message)
        {
            if (message is ChatCompletedEvent completedEvent)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ChatMessages.Last().MessageCompleted = true;
                    StatusMessage = $"Prompt Tokens: {completedEvent.PromptTokens}, Response Tokens: {completedEvent.ResponseTokens}";
                    if (SelectedConversation != null)
                    {
                        _messageService.Publish(new RefreshConversationRequest(SelectedConversation.Id));
                    }
                });
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
                        Conversations = new ObservableCollection<Conversation>(Conversations);
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
                var userMessage = new UCChatMessage(null)
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

        private void UserPrompt_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                Send_Click(sender, e);
            }
        }

        private async void CreateConversation_Click(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new CreateConversationsRequest());
        }

        private void RenameConversation_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedConversation != null)
            {
                var window = new StringInputWindow("Rename Conversation", "Enter new conversation title:", SelectedConversation.Title ?? string.Empty);
                window.Owner = this;
                window.ShowDialog();
                if (window.Result == UI.Enums.DialogResult.OK)
                {
                    if (!string.IsNullOrWhiteSpace(window.Value))
                    {
                        _messageService.Publish(new UpdateConversationTitleRequest(SelectedConversation.Id, window.Value));
                    }
                }
            }
        }

        private void DeleteConversation_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedConversation != null)
            {
                var result = MessageBox.Show("Are you sure you want to delete this conversation?", "Delete Conversation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _messageService.Publish(new DeleteConversationRequest(SelectedConversation.Id));
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ManageDocuments_Click(object sender, RoutedEventArgs e)
        {
            var window = new DocumentsWindow(_messageService)
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void ManageModelContextWindows_Click(object sender, RoutedEventArgs e)
        {
            var window = new ModelContextWindowsWindow(_messageService)
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void RefreshConversation_Click(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new GetConversationsRequest());
        }
    }
}