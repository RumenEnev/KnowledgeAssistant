using KnowledgeAssistant.Wpf.Messages.Documents;
using KnowledgeAssistant.Wpf.Models;
using MessageServices;
using MessageServices.Messages;
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
        private int? _editingDocumentId;
        private bool _isSaving;

        public DocumentsWindow(MessageService messageService)
        {
            InitializeComponent();
            DataContext = this;

            _messageService = messageService;
            _messageService.Subscribe<DocumentsUpdatedEvent>(this, DocumentsUpdatedEventReceived);
            _messageService.Subscribe<TopicsUpdatedEvent>(this, TopicsUpdatedEventReceived);
            _messageService.Subscribe<DocumentAddedEvent>(this, DocumentAddedEventReceived);
            _messageService.Subscribe<DocumentUpdatedEvent>(this, DocumentUpdatedEventReceived);
            _messageService.Subscribe<DocumentDeletedEvent>(this, DocumentDeletedEventReceived);
            _messageService.Subscribe<UserMessage>(this, UserMessageReceived);
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

        public int? EditingDocumentId
        {
            get => _editingDocumentId;
            set
            {
                _editingDocumentId = value;
                OnPropertyChanged(nameof(EditingDocumentId));
                OnPropertyChanged(nameof(FormHeader));
                OnPropertyChanged(nameof(SubmitButtonText));
                OnPropertyChanged(nameof(CancelButtonVisibility));
            }
        }

        public string FormHeader => EditingDocumentId is null ? "Add Document" : "Edit Document";

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
                    return EditingDocumentId is null ? "Adding..." : "Updating...";

                return EditingDocumentId is null ? "Add Document" : "Update Document";
            }
        }

        public Visibility CancelButtonVisibility => EditingDocumentId is null ? Visibility.Collapsed : Visibility.Visible;

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
                            OriginalText = document.OriginalText,
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
                Application.Current.Dispatcher.Invoke(ClearForm);
            }
        }

        private void DocumentUpdatedEventReceived(MessageBase message)
        {
            if (message is DocumentUpdatedEvent @event)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ClearForm();
                    StatusMessage = "Document updated.";
                });
            }
        }

        private void UserMessageReceived(MessageBase message)
        {
            if (message is UserMessage { Title: "Add Document Failed" or "Update Document Failed" })
            {
                Application.Current.Dispatcher.Invoke(() => IsSaving = false);
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

                    if (EditingDocumentId == @event.DocumentId)
                    {
                        ClearForm();
                    }
                });
            }
        }

        private void AddDocument_Click(object sender, RoutedEventArgs e)
        {
            if (IsSaving)
            {
                return;
            }

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

            IsSaving = true;

            if (EditingDocumentId is int documentId)
            {
                _messageService.Publish(new UpdateDocumentRequest(documentId, title, text, topics));
            }
            else
            {
                _messageService.Publish(new AddDocumentRequest(title, text, topics));
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            documentsList.SelectedItem = null;
        }

        private void ClearForm()
        {
            EditingDocumentId = null;
            NewTitle = string.Empty;
            NewText = string.Empty;
            IsSaving = false;
            foreach (var topic in AvailableTopics)
            {
                topic.IsSelected = false;
            }
        }

        private void DocumentsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is not System.Windows.Controls.ListView { SelectedItem: DocumentDisplayModel document })
            {
                return;
            }

            EditingDocumentId = document.Id;
            NewTitle = document.Title;
            NewText = document.OriginalText;

            var documentTopics = new HashSet<string>(document.Topics, StringComparer.OrdinalIgnoreCase);
            foreach (var topic in AvailableTopics)
            {
                topic.IsSelected = documentTopics.Contains(topic.Name);
            }
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
