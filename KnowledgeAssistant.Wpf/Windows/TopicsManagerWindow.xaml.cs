using KnowledgeAssistant.Domain.Documents;
using KnowledgeAssistant.Wpf.Messages.Documents;
using MessageServices;
using MessageServices.Messages;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace KnowledgeAssistant.Wpf.Windows
{
    public partial class TopicsManagerWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;

        private string? _newTopicName;
        private string? _statusMessage;
        private int? _editingTopicId;
        private bool _isSaving;

        public TopicsManagerWindow(MessageService messageService)
        {
            InitializeComponent();
            DataContext = this;

            _messageService = messageService;
            _messageService.Subscribe<TopicsUpdatedEvent>(this, TopicsUpdatedEventReceived);
            _messageService.Subscribe<UserMessage>(this, UserMessageReceived);
        }

        public ObservableCollection<Topic> Topics { get; } = new ObservableCollection<Topic>();

        public string? NewTopicName
        {
            get => _newTopicName;
            set { _newTopicName = value; OnPropertyChanged(nameof(NewTopicName)); }
        }

        public string? StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public int? EditingTopicId
        {
            get => _editingTopicId;
            set
            {
                _editingTopicId = value;
                OnPropertyChanged(nameof(EditingTopicId));
                OnPropertyChanged(nameof(FormHeader));
                OnPropertyChanged(nameof(SubmitButtonText));
                OnPropertyChanged(nameof(CancelButtonVisibility));
            }
        }

        public string FormHeader => EditingTopicId is null ? "Add Topic" : "Rename Topic";

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                _isSaving = value;
                OnPropertyChanged(nameof(IsSaving));
                OnPropertyChanged(nameof(SubmitButtonText));
                OnPropertyChanged(nameof(CanSubmit));
            }
        }

        public bool CanSubmit => !IsSaving;

        public string SubmitButtonText
        {
            get
            {
                if (IsSaving)
                    return EditingTopicId is null ? "Adding..." : "Saving...";

                return EditingTopicId is null ? "Add Topic" : "Save";
            }
        }

        public Visibility CancelButtonVisibility => EditingTopicId is null ? Visibility.Collapsed : Visibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void TopicsManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new GetTopicsRequest());
        }

        private void TopicsUpdatedEventReceived(MessageBase message)
        {
            if (message is TopicsUpdatedEvent @event)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Topics.Clear();
                    foreach (var topic in @event.Topics.OrderBy(t => t.Name))
                    {
                        Topics.Add(topic);
                    }

                    IsSaving = false;
                    StatusMessage = $"{Topics.Count} topic(s) loaded.";
                });
            }
        }

        private void UserMessageReceived(MessageBase message)
        {
            if (message is UserMessage { Title: "Add Topic Failed" or "Update Topic Failed" or "Delete Topic Failed" })
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsSaving = false);
            }
        }

        private void SubmitTopic_Click(object sender, RoutedEventArgs e)
        {
            if (IsSaving)
            {
                return;
            }

            var name = NewTopicName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Topic name is required.", "Manage Topics", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSaving = true;
            if (EditingTopicId is int topicId)
            {
                _messageService.Publish(new UpdateTopicRequest(topicId, name));
            }
            else
            {
                _messageService.Publish(new CreateTopicRequest(name));
            }

            ClearForm();
        }

        private void EditTopic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: Topic topic })
            {
                return;
            }

            EditingTopicId = topic.Id;
            NewTopicName = topic.Name;
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            EditingTopicId = null;
            NewTopicName = string.Empty;
        }

        private void DeleteTopic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: Topic topic })
            {
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete '{topic.Name}'?", "Delete Topic", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            if (EditingTopicId == topic.Id)
            {
                ClearForm();
            }

            _messageService.Publish(new DeleteTopicRequest(topic.Id));
        }
    }
}
