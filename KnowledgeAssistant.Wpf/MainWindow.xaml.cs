using Infrastructure.Dto;
using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Infrastructure.Dto;
using KnowledgeAssistant.Wpf.Messages;
using KnowledgeAssistant.Wpf.Messages.Conversations;
using KnowledgeAssistant.Wpf.Messages.Documentation;
using KnowledgeAssistant.Wpf.Messages.ModelsManagement;
using KnowledgeAssistant.Wpf.Models;
using KnowledgeAssistant.Wpf.UserControls;
using KnowledgeAssistant.Wpf.Views;
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
        private bool _suppressProviderUpdate;
        private bool _suppressModelUpdate;
        private bool _persistModelAfterProviderChange;
        private string? _pendingConversationProvider;
        private string? _pendingConversationModel;
        private string? _selectedModel;
        private string? _selectedProvider;
        private string? _userPrompt;
        private string? _statusMessage;
        private Thickness _chatMessageMargin;
        private Conversation? _selectedConversation;
        private Guid? _lastConversationId;
        private ObservableCollection<string> _models = new ObservableCollection<string>();
        private ObservableCollection<string> _providers = new ObservableCollection<string>();
        private List<AvailableModelInfo> _allModels = new List<AvailableModelInfo>();
        private bool _showOnlyToolCallingModels;
        private ObservableCollection<Conversation> _conversations = new ObservableCollection<Conversation>();
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
            _messageService.Subscribe<DocumentationReadyEvent>(this, DocumentationReadyEventReceived);
            _messageService.Subscribe<AvailableProvidersUpdatedEvent>(this, AvailableProvidersUpdatedEventReceived);
        }

        public string? SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (string.Equals(_selectedProvider, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _selectedProvider = value;
                OnPropertyChanged(nameof(SelectedProvider));

                _allModels.Clear();
                Models = new ObservableCollection<string>();
                SetSelectedModel(null, persist: false);

                if (_suppressProviderUpdate || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _pendingConversationProvider = null;
                _pendingConversationModel = null;
                _persistModelAfterProviderChange = SelectedConversation is not null;

                StatusMessage = $"Loading models from {value}...";
                _messageService.Publish(new GetAvailableModelsRequest(value));
            }
        }

        public string? SelectedModel
        {
            get => _selectedModel;
            set => SetSelectedModel(value, persist: true);
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
                if (_selectedConversation == value)
                {
                    return;
                }

                _selectedConversation = value;
                _pendingConversationProvider = null;
                _pendingConversationModel = null;
                _persistModelAfterProviderChange = false;

                OnPropertyChanged(nameof(SelectedConversation));

                if (_selectedConversation is not null)
                {
                    _messageService.Publish(
                        new SelectedConversationChangedRequest(_selectedConversation.Id));
                }
            }
        }

        public ObservableCollection<string> Models
        {
            get => _models;
            set
            {
                if (ReferenceEquals(_models, value))
                {
                    return;
                }

                _models = value;
                OnPropertyChanged(nameof(Models));
                OnPropertyChanged(nameof(HasModels));
            }
        }

        public ObservableCollection<string> Providers
        {
            get => _providers;
            set
            {
                if (ReferenceEquals(_providers, value))
                {
                    return;
                }

                _providers = value;
                OnPropertyChanged(nameof(Providers));
                OnPropertyChanged(nameof(HasProviders));
            }
        }

        public bool ShowOnlyToolCallingModels
        {
            get => _showOnlyToolCallingModels;
            set
            {
                if (_showOnlyToolCallingModels != value)
                {
                    _showOnlyToolCallingModels = value;
                    OnPropertyChanged(nameof(ShowOnlyToolCallingModels));
                    RecalculateModels(persistFallback: true);
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

        public bool HasProviders => Providers.Count > 0;

        public bool HasModels => Models.Count > 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetSelectedModel(string? value, bool persist)
        {
            if (string.Equals(_selectedModel, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectedModel = value;
            OnPropertyChanged(nameof(SelectedModel));

            if (!persist ||
                _suppressModelUpdate ||
                string.IsNullOrWhiteSpace(value) ||
                string.IsNullOrWhiteSpace(SelectedProvider) ||
                SelectedConversation is null)
            {
                return;
            }

            _messageService.Publish(new UpdateConversationModelSelectionRequest(SelectedConversation.Id, SelectedProvider, value));
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
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var conversationToUpdate = Conversations.FirstOrDefault(c => c.Id == request.Conversation.Id);
                    if (conversationToUpdate != null)
                    {
                        conversationToUpdate.Title = request.Conversation.Title;
                        conversationToUpdate.TopicId = request.Conversation.TopicId;
                        conversationToUpdate.Topic = request.Conversation.Topic;
                        Conversations = new ObservableCollection<Conversation>(Conversations);
                        SelectedConversation = conversationToUpdate;
                    }
                });
            }
        }

        private void UpdateConversationMessagesReceived(MessageBase message)
        {
            if (message is not UpdateConversationMessages request ||
                request.Conversation is null)
            {
                return;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ChatMessages.Clear();

                var conversation = request.Conversation;
                _pendingConversationProvider = conversation.Provider;
                _pendingConversationModel = conversation.SelectedModel;
                _persistModelAfterProviderChange = false;

                var provider = conversation.Provider;
                if (string.IsNullOrWhiteSpace(provider) ||
                    string.Equals(provider, ModelProviderNames.Unknown, StringComparison.OrdinalIgnoreCase))
                {
                    provider = Providers.FirstOrDefault();
                }

                if (!string.IsNullOrWhiteSpace(provider))
                {
                    _allModels.Clear();
                    Models = new ObservableCollection<string>();
                    SetSelectedModel(null, persist: false);

                    _suppressProviderUpdate = true;
                    try
                    {
                        SelectedProvider = provider;
                    }
                    finally
                    {
                        _suppressProviderUpdate = false;
                    }

                    StatusMessage = $"Loading models from {provider}...";
                    _messageService.Publish(new GetAvailableModelsRequest(provider));
                }

                if (conversation.Messages?.Any() == true)
                {
                    foreach (var msg in conversation.Messages)
                    {
                        ChatMessages.Add(new UCChatMessage(null)
                        {
                            Message = msg.Content,
                            IsUserMessage = msg.Role == "user",
                            MessageCompleted = true
                        });
                    }
                }
            });
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
            if (message is not AvailableModelsUpdatedEvent modelsEvent)
            {
                return;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Ignore a late response for a provider that is no longer selected.
                if (!string.Equals(
                        modelsEvent.Provider,
                        SelectedProvider,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _allModels = modelsEvent.Models.ToList();

                var isApplyingConversationSelection =
                    !string.IsNullOrWhiteSpace(_pendingConversationModel);

                RecalculateModels(
                    persistFallback:
                        _persistModelAfterProviderChange &&
                        !isApplyingConversationSelection);

                _pendingConversationProvider = null;
                _pendingConversationModel = null;
                _persistModelAfterProviderChange = false;

                StatusMessage = Models.Count == 0
                    ? $"No models are available from {modelsEvent.Provider}."
                    : $"{Models.Count} models loaded from {modelsEvent.Provider}.";
            });
        }

        private void RecalculateModels(bool persistFallback = false)
        {
            var preferredModel = !string.IsNullOrWhiteSpace(_pendingConversationModel)
                ? _pendingConversationModel
                : SelectedModel;

            var filteredModels = _allModels
                .Where(model => !ShowOnlyToolCallingModels || model.CanCallTools)
                .Select(model => model.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name)
                .ToList();

            Models = new ObservableCollection<string>(filteredModels);

            var newSelectedModel =
                !string.IsNullOrWhiteSpace(preferredModel) &&
                Models.Contains(preferredModel)
                    ? preferredModel
                    : Models.FirstOrDefault();

            SetSelectedModel(newSelectedModel, persistFallback);
        }

        private void DocumentationReadyEventReceived(MessageBase message)
        {
            if (message is DocumentationReadyEvent request)
            {
                try
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var window = new DocumentationPreviewWindow(_messageService, request.OutputPath, request.Title)
                        {
                            Owner = this
                        };

                        window.Show();
                    });
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error opening documentation preview window: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ChatCompletedEventReceived(MessageBase message)
        {
            if (message is ChatCompletedEvent completedEvent)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                            Provider = ModelProviderNames.Unknown
                        };

                        Conversations.Add(conversation);
                        SelectedConversation = conversation;
                    }
                });
            }
        }

        private void AvailableProvidersUpdatedEventReceived(MessageBase message)
        {
            if (message is not AvailableProvidersUpdatedEvent providersEvent)
            {
                return;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var providers = providersEvent.Providers
                    .Where(provider => !string.IsNullOrWhiteSpace(provider))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(provider => provider)
                    .ToList();

                Providers = new ObservableCollection<string>(providers);

                if (Providers.Count == 0)
                {
                    _pendingConversationProvider = null;
                    _pendingConversationModel = null;
                    _persistModelAfterProviderChange = false;

                    _suppressProviderUpdate = true;
                    try
                    {
                        SelectedProvider = null;
                    }
                    finally
                    {
                        _suppressProviderUpdate = false;
                    }

                    StatusMessage = "No AI model providers are available.";
                    return;
                }

                var requestedProvider = !string.IsNullOrWhiteSpace(_pendingConversationProvider) &&
                                        Providers.Contains(
                                            _pendingConversationProvider,
                                            StringComparer.OrdinalIgnoreCase)
                    ? _pendingConversationProvider
                    : SelectedProvider;

                var selectedProvider = !string.IsNullOrWhiteSpace(requestedProvider) &&
                                       Providers.Contains(
                                           requestedProvider,
                                           StringComparer.OrdinalIgnoreCase)
                    ? requestedProvider
                    : Providers.First();

                _allModels.Clear();
                Models = new ObservableCollection<string>();
                SetSelectedModel(null, persist: false);
                _persistModelAfterProviderChange = false;

                _suppressProviderUpdate = true;
                try
                {
                    SelectedProvider = selectedProvider;
                }
                finally
                {
                    _suppressProviderUpdate = false;
                }

                StatusMessage = $"Loading models from {selectedProvider}...";
                _messageService.Publish(
                    new GetAvailableModelsRequest(selectedProvider));
            });
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StatusMessage = "Loading AI model providers...";
            _messageService.Publish(new GetAvailableProvidersRequest());
            _messageService.Publish(new GetConversationsRequest());
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(UserPrompt))
            {
                var userMessage = new UCChatMessage(null)
                {
                    Message = UserPrompt,
                    MessageCompleted = true,
                    IsUserMessage = true
                };

                ChatMessages.Add(userMessage);
                ChatMessages.Add(new UCChatMessage(_messageService));
                _messageService.Publish(new SendUserMessageRequest(UserPrompt));
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

        private void SetConversationTopic_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedConversation != null)
            {
                var window = new TopicSelectionWindow(_messageService, SelectedConversation.TopicId)
                {
                    Owner = this
                };
                window.ShowDialog();
                if (window.Confirmed)
                {
                    _messageService.Publish(new UpdateConversationTopicRequest(SelectedConversation.Id, window.SelectedTopicId));
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
            System.Windows.Application.Current.Shutdown();
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

        private void ManageTopics_Click(object sender, RoutedEventArgs e)
        {
            var window = new TopicsManagerWindow(_messageService)
            {
                Owner = this
            };

            window.ShowDialog();
            _messageService.Publish(new GetConversationsRequest());
        }

        private void RefreshConversation_Click(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new GetConversationsRequest());
        }

        private void SetApiUrl_Click(object sender, RoutedEventArgs e)
        {
            var window = new StringInputWindow("Base URL", "Setup base URL:");
            window.Owner = this;
            window.ShowDialog();
            if (window.Result == UI.Enums.DialogResult.OK)
            {
                _messageService.Publish(new UpdateApiUrlRequest(window.Value));
            }
        }

        private void ManageRepositories_Click(object sender, RoutedEventArgs e)
        {
            var window = new RepositoriesManagerWindow(_messageService)
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void ManageTools_Click(object sender, RoutedEventArgs e)
        {
            var window = new ToolsManagerWindow(_messageService)
            {
                Owner = this
            };

            window.ShowDialog();
        }
    }
}