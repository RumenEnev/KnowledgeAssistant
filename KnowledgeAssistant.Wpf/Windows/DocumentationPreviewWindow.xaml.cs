using KnowledgeAssistant.Wpf.Markdown;
using KnowledgeAssistant.Wpf.Messages.Documentation;
using MessageServices;
using System.ComponentModel;
using System.Windows;

namespace KnowledgeAssistant.Wpf.Windows
{
    public partial class DocumentationPreviewWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;
        private readonly Guid _correlationId;
        private readonly Guid _repositoryId;
        private readonly string _relativeFilePath;
        private readonly string _title;
        private string _markdown = string.Empty;
        private string? _statusMessage;
        private bool _canSave = true;

        public DocumentationPreviewWindow(
            MessageService messageService,
            Guid correlationId,
            Guid repositoryId,
            string repositoryName,
            string fileName,
            string relativeFilePath,
            string title,
            string markdown)
        {
            InitializeComponent();
            DataContext = this;

            _messageService = messageService;
            _correlationId = correlationId;
            _repositoryId = repositoryId;
            _relativeFilePath = relativeFilePath;
            _title = title;
            Markdown = markdown;
            WindowTitle = "Generated Documentation";
            SubHeaderText = $"Documentation generated for '{fileName}' in repository '{repositoryName}'. Review it below, then Save & Ingest or Close to discard.";

            _messageService.Subscribe<DocumentationSavedEvent>(this, DocumentationSavedEventReceived);
            _messageService.Subscribe<DocumentationSaveFailedEvent>(this, DocumentationSaveFailedEventReceived);
        }

        public string WindowTitle { get; }

        public string SubHeaderText { get; }

        public string Markdown
        {
            get => _markdown;
            set
            {
                _markdown = MathDelimiterNormalizer.Normalize(value) ?? string.Empty;
                OnPropertyChanged(nameof(Markdown));
            }
        }

        public string? StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

        public bool CanSave
        {
            get => _canSave;
            set
            {
                _canSave = value;
                OnPropertyChanged(nameof(CanSave));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SaveAndIngest_Click(object sender, RoutedEventArgs e)
        {
            CanSave = false;
            StatusMessage = "Saving and ingesting documentation...";
            _messageService.Publish(new SaveDocumentationRequest(_correlationId, _repositoryId, _relativeFilePath, _title, Markdown));
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DocumentationSavedEventReceived(MessageBase message)
        {
            if (message is DocumentationSavedEvent request && request.CorrelationId == _correlationId)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Saved to {request.SavedFilePath} and ingested into the RAG index.";
                    CanSave = false;
                });
            }
        }

        private void DocumentationSaveFailedEventReceived(MessageBase message)
        {
            if (message is DocumentationSaveFailedEvent request && request.CorrelationId == _correlationId)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Failed to save documentation: {request.ErrorMessage}";
                    CanSave = true;
                });
            }
        }
    }
}
