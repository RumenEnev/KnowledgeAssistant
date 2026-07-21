using KnowledgeAssistant.Wpf.Messages.Documents;
using KnowledgeAssistant.Wpf.Models;
using MessageServices;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace KnowledgeAssistant.Wpf.Windows
{
    public partial class DocumentsWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;

        private string? _newTitle;
        private string? _newText;
        private string? _statusMessage;

        public DocumentsWindow(MessageService messageService)
        {
            InitializeComponent();
            DataContext = this;

            _messageService = messageService;
            _messageService.Subscribe<DocumentsUpdatedEvent>(this, DocumentsUpdatedEventReceived);
            _messageService.Subscribe<TopicsUpdatedEvent>(this, TopicsUpdatedEventReceived);
            _messageService.Subscribe<DocumentAddedEvent>(this, DocumentAddedEventReceived);
            _messageService.Subscribe<DocumentDeletedEvent>(this, DocumentDeletedEventReceived);
        }

        public ObservableCollection<DocumentDisplayModel> Documents { get; } = new ObservableCollection<DocumentDisplayModel>();

        public ObservableCollection<TopicSelectionItem> AvailableTopics { get; } = new ObservableCollection<TopicSelectionItem>();

        public string? NewTitle
        {
            get => _newTitle;
            set { _newTitle = value; OnPropertyChanged(nameof(NewTitle)); }
        }

        public string? NewText
        {
            get => _newText;
            set { _newText = value; OnPropertyChanged(nameof(NewText)); }
        }

        public string? StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void DocumentsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new GetDocumentsRequest());
            _messageService.Publish(new GetTopicsRequest());
        }

        private void DocumentsUpdatedEventReceived(MessageBase message)
        {
            if (message is DocumentsUpdatedEvent @event)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Documents.Clear();
                    foreach (var document in @event.Documents)
                    {
                        Documents.Add(new DocumentDisplayModel
                        {
                            Id = document.Id,
                            Title = document.Title,
                            Topics = document.Topics
                        });
                    }

                    StatusMessage = $"{Documents.Count} document(s) loaded.";
                });
            }
        }

        private void TopicsUpdatedEventReceived(MessageBase message)
        {
            if (message is TopicsUpdatedEvent @event)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AvailableTopics.Clear();
                    foreach (var topic in @event.Topics)
                    {
                        AvailableTopics.Add(new TopicSelectionItem { Id = topic.Id, Name = topic.Name });
                    }
                });
            }
        }

        private void DocumentAddedEventReceived(MessageBase message)
        {
            if (message is DocumentAddedEvent)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    NewTitle = string.Empty;
                    NewText = string.Empty;
                    foreach (var topic in AvailableTopics)
                    {
                        topic.IsSelected = false;
                    }
                });
            }
        }

        private void DocumentDeletedEventReceived(MessageBase message)
        {
            if (message is DocumentDeletedEvent @event)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var document = Documents.FirstOrDefault(d => d.Id == @event.DocumentId);
                    if (document != null)
                    {
                        Documents.Remove(document);
                        StatusMessage = $"Deleted '{document.Title}'.";
                    }
                });
            }
        }

        private void AddDocument_Click(object sender, RoutedEventArgs e)
        {
            var title = NewTitle?.Trim();
            var text = NewText?.Trim();
            var topics = AvailableTopics
                .Where(t => t.IsSelected)
                .Select(t => t.Name)
                .ToList();

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text) || topics.Count == 0)
            {
                MessageBox.Show("Title, text and at least one topic are required.", "Add Document", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _messageService.Publish(new AddDocumentRequest(title, text, topics));
        }

        private void LoadTextFromFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Select a .txt file"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                NewText = File.ReadAllText(dialog.FileName);
                NewTitle = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read the file: {ex.Message}", "Load Text File Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteDocument_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: DocumentDisplayModel document })
            {
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete '{document.Title}'?", "Delete Document", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            _messageService.Publish(new DeleteDocumentRequest(document.Id));
        }
    }
}
