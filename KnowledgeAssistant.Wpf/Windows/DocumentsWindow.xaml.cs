using KnowledgeAssistant.Contracts.Enums;
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
        private int _chunkTargetSizeChars = 1000;
        private int _chunkOverlapChars = 150;
        private bool _isSavingChunkingSettings;
        private DocumentType _documentType = DocumentType.PlainText;

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
            _messageService.Subscribe<ChunkingSettingsUpdatedEvent>(this, ChunkingSettingsUpdatedEventReceived);
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

        public int ChunkTargetSizeChars
        {
            get => _chunkTargetSizeChars;
            set { _chunkTargetSizeChars = value; OnPropertyChanged(nameof(ChunkTargetSizeChars)); }
        }

        public int ChunkOverlapChars
        {
            get => _chunkOverlapChars;
            set { _chunkOverlapChars = value; OnPropertyChanged(nameof(ChunkOverlapChars)); }
        }

        public bool IsSavingChunkingSettings
        {
            get => _isSavingChunkingSettings;
            set
            {
                _isSavingChunkingSettings = value;
                OnPropertyChanged(nameof(IsSavingChunkingSettings));
                OnPropertyChanged(nameof(SaveChunkingSettingsButtonText));
                OnPropertyChanged(nameof(CanSaveChunkingSettings));
            }
        }

        public bool CanSaveChunkingSettings => !IsSavingChunkingSettings;

        public string SaveChunkingSettingsButtonText => IsSavingChunkingSettings ? "Saving..." : "Save Settings";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void DocumentsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new GetDocumentsRequest());
            _messageService.Publish(new GetTopicsRequest());
            _messageService.Publish(new GetChunkingSettingsRequest());
        }

        private void DocumentsUpdatedEventReceived(MessageBase message)
        {
            if (message is DocumentsUpdatedEvent @event)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                System.Windows.Application.Current.Dispatcher.Invoke(ClearForm);
            }
        }

        private void DocumentUpdatedEventReceived(MessageBase message)
        {
            if (message is DocumentUpdatedEvent @event)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsSaving = false);
            }
            else if (message is UserMessage { Title: "Save Chunking Settings Failed" })
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsSavingChunkingSettings = false);
            }
        }

        private void ChunkingSettingsUpdatedEventReceived(MessageBase message)
        {
            if (message is ChunkingSettingsUpdatedEvent @event)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ChunkTargetSizeChars = @event.ChunkTargetSizeChars;
                    ChunkOverlapChars = @event.ChunkOverlapChars;
                    IsSavingChunkingSettings = false;
                });
            }
        }

        private void DocumentDeletedEventReceived(MessageBase message)
        {
            if (message is DocumentDeletedEvent @event)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                _messageService.Publish(new UpdateDocumentRequest(documentId, title, text, _documentType, topics));
            }
            else
            {
                _messageService.Publish(new AddDocumentRequest(title, text, _documentType, topics));
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            documentsList.SelectedItem = null;
            _documentType = DocumentType.PlainText;
        }

        private void ClearForm()
        {
            EditingDocumentId = null;
            NewTitle = string.Empty;
            NewText = string.Empty;
            IsSaving = false;
            _documentType = DocumentType.PlainText;
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
                Filter = "Text files (*.txt), Markdown files (*.md)|*.txt;*.md|All files (*.*)|*.*",
                Title = "Select a .txt or .md file"
            };

            if (dialog.ShowDialog(this) == true)
            {
                try
                {
                    NewText = File.ReadAllText(dialog.FileName);
                    NewTitle = Path.GetFileNameWithoutExtension(dialog.FileName);
                    _documentType = Path.GetExtension(dialog.FileName).Equals(".md", StringComparison.OrdinalIgnoreCase)
                        ? DocumentType.Markdown
                        : DocumentType.PlainText;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to read the file: {ex.Message}", "Load Text File Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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

        private void SaveChunkingSettings_Click(object sender, RoutedEventArgs e)
        {
            if (IsSavingChunkingSettings)
            {
                return;
            }

            if (ChunkTargetSizeChars <= 0)
            {
                MessageBox.Show("Chunk size must be greater than zero.", "Chunking Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ChunkOverlapChars < 0 || ChunkOverlapChars >= ChunkTargetSizeChars)
            {
                MessageBox.Show("Chunk overlap must be zero or greater, and smaller than the chunk size.", "Chunking Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSavingChunkingSettings = true;
            _messageService.Publish(new UpdateChunkingSettingsRequest(ChunkTargetSizeChars, ChunkOverlapChars));
        }
    }
}
