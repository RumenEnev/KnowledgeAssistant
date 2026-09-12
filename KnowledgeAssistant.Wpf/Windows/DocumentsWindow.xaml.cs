using KnowledgeAssistant.Contracts.Enums;
using KnowledgeAssistant.Domain.Documents;
using KnowledgeAssistant.Wpf.Messages.Documents;
using KnowledgeAssistant.Wpf.Models;
using MessageServices;
using MessageServices.Messages;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace KnowledgeAssistant.Wpf.Windows;

public partial class DocumentsWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
{
    private readonly MessageService _messageService;
    private int _chunkSize;
    private int _chunkOverlap;
    private string? _newTitle;
    private string? _newText;
    private string? _statusMessage;
    private string? _embeddingModel;
    private int? _editingDocumentId;
    private bool _isSaving;
    private bool _isSavingRetrievalConfig;
    private DocumentType _documentType = DocumentType.PlainText;
    private ObservableCollection<string> _embeddingModels = new ObservableCollection<string>();

    public DocumentsWindow(MessageService messageService)
    {
        InitializeComponent();
        DataContext = this;

        _messageService = messageService;
        _messageService.Subscribe<EmbeddingsLoadedEvent>(this, EmbeddingsLoadedEventReceived);
        _messageService.Subscribe<DocumentsUpdatedEvent>(this, DocumentsUpdatedEventReceived);
        _messageService.Subscribe<TopicsUpdatedEvent>(this, TopicsUpdatedEventReceived);
        _messageService.Subscribe<DocumentAddedEvent>(this, DocumentAddedEventReceived);
        _messageService.Subscribe<DocumentUpdatedEvent>(this, DocumentUpdatedEventReceived);
        _messageService.Subscribe<DocumentDeletedEvent>(this, DocumentDeletedEventReceived);
        _messageService.Subscribe<UserMessage>(this, UserMessageReceived);
    }

    public bool IsDocumentSelected => EditingDocumentId is not null;

    public ObservableCollection<DocumentDisplayModel> Documents { get; } = new ObservableCollection<DocumentDisplayModel>();

    public ObservableCollection<TopicSelectionItem> AvailableTopics { get; } = new ObservableCollection<TopicSelectionItem>();

    public ObservableCollection<TopicSelectionNode> TopicTree { get; } = new ObservableCollection<TopicSelectionNode>();

    public ObservableCollection<string> EmbeddingModels
    {
        get => _embeddingModels;
        set
        {
            _embeddingModels = value;
            OnPropertyChanged(nameof(EmbeddingModels));
        }
    }
    public string? EmbeddingModel
    {
        get => _embeddingModel;
        set
        {
            _embeddingModel = value;
            OnPropertyChanged(nameof(EmbeddingModel));
        }
    }

    public string? NewTitle
    {
        get => _newTitle;
        set
        {
            _newTitle = value;
            OnPropertyChanged(nameof(NewTitle));
        }
    }

    public string? NewText
    {
        get => _newText;
        set
        {
            _newText = value;
            OnPropertyChanged(nameof(NewText));
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
        }
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
            OnPropertyChanged(nameof(IsDocumentSelected));
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
            {
                return EditingDocumentId is null ? "Adding..." : "Updating...";
            }

            return EditingDocumentId is null ? "Add Document" : "Update Document";
        }
    }

    public Visibility CancelButtonVisibility => EditingDocumentId is null ? Visibility.Collapsed : Visibility.Visible;

    public string SelectedTopicsSummary
    {
        get
        {
            var selected = AvailableTopics.Where(t => t.IsSelected).Select(t => t.Name).ToList();
            return selected.Count == 0 ? "Select topics..." : string.Join(", ", selected);
        }
    }

    public int ChunkSize
    {
        get => _chunkSize;
        set
        {
            _chunkSize = value;
            OnPropertyChanged(nameof(ChunkSize));
        }
    }

    public int ChunkOverlap
    {
        get => _chunkOverlap;
        set
        {
            _chunkOverlap = value;
            OnPropertyChanged(nameof(ChunkOverlap));
        }
    }

    public string SaveRetrievalConfigButtonText => _isSavingRetrievalConfig ? "Saving..." : "Save Settings";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void DocumentsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _messageService.Publish(new GetDocumentsRequest());
        _messageService.Publish(new GetTopicsRequest());
        _messageService.Publish(new GetEmbeddingsModelsRequest());
    }

    private void EmbeddingsLoadedEventReceived(MessageBase message)
    {
        if (message is EmbeddingsLoadedEvent @event)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                EmbeddingModels = new ObservableCollection<string>(@event.Models);
                EmbeddingModel = @event.SelectedModel;
                ChunkOverlap = @event.Overlap;
                ChunkSize = @event.ChunkSize;
            });
        }
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
                var previouslySelectedIds = new HashSet<int>(AvailableTopics.Where(t => t.IsSelected).Select(t => t.Id));
                foreach (var existing in AvailableTopics)
                {
                    existing.PropertyChanged -= TopicItem_PropertyChanged;
                }

                AvailableTopics.Clear();
                foreach (var topic in @event.Topics)
                {
                    var item = new TopicSelectionItem
                    {
                        Id = topic.Id,
                        Name = topic.Name,
                        ParentId = topic.ParentId,
                        IsSelected = previouslySelectedIds.Contains(topic.Id)
                    };
                    item.PropertyChanged += TopicItem_PropertyChanged;
                    AvailableTopics.Add(item);
                }

                RebuildTopicTree();
                OnPropertyChanged(nameof(SelectedTopicsSummary));
            });
        }
    }

    private void TopicItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TopicSelectionItem.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedTopicsSummary));
        }
    }

    private void RebuildTopicTree()
    {
        var wasExpanded = new HashSet<int>();
        CollectExpandedIds(TopicTree, wasExpanded);

        TopicTree.Clear();

        var nodesById = AvailableTopics.ToDictionary(t => t.Id, t => new TopicSelectionNode(t));

        foreach (var node in nodesById.Values.OrderBy(n => n.Name))
        {
            node.IsExpanded = wasExpanded.Count == 0 || wasExpanded.Contains(node.Id);

            if (node.Item.ParentId is int parentId && nodesById.TryGetValue(parentId, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
            else
            {
                TopicTree.Add(node);
            }
        }
    }

    private static void CollectExpandedIds(IEnumerable<TopicSelectionNode> nodes, HashSet<int> result)
    {
        foreach (var node in nodes)
        {
            if (node.IsExpanded)
            {
                result.Add(node.Id);
            }

            CollectExpandedIds(node.Children, result);
        }
    }

    private void DocumentAddedEventReceived(MessageBase message)
    {
        if (message is DocumentAddedEvent)
        {
            if (message is DocumentAddedEvent @event)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ClearForm();
                    MessageBox.Show($"Document added successfully. Chunks count: {@event.ChunksCount}", "Add Document", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
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

    private async void DocumentsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ListView { SelectedItem: DocumentDisplayModel document })
        {
            return;
        }

        EditingDocumentId = document.Id;
        NewTitle = document.Title;
        NewText = document.OriginalText;
        OnPropertyChanged(nameof(IsDocumentSelected));
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

    private void SaveRetrievalConfig_Click(object sender, RoutedEventArgs e)
    {
        if (_isSavingRetrievalConfig)
        {
            return;
        }

        if (ChunkOverlap >= ChunkSize || ChunkOverlap < 0)
        {
            MessageBox.Show("Chunk overlap must be zero or greater, and smaller than chunk size.", "Retrieval Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isSavingRetrievalConfig = true;
        OnPropertyChanged(nameof(SaveRetrievalConfigButtonText));
        _messageService.Publish(new UpdateChunkingSettingsRequest(EmbeddingModel ?? string.Empty, ChunkSize, ChunkOverlap));
    }

    private void ResetRetrievalConfig_Click(object sender, RoutedEventArgs e)
    {
        ChunkSize = 1200;
        ChunkOverlap = 200;
    }
}