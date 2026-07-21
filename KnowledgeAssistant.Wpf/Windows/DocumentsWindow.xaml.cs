using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Domain.Documents;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace KnowledgeAssistant.Wpf.Windows
{
    public class DocumentDisplayModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public IEnumerable<string> Topics { get; set; } = Array.Empty<string>();

        public string TopicsDisplay => string.Join(", ", Topics);
    }

    /// <summary>Selectable topic shown as a checkbox item when adding a document.</summary>
    public class TopicSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class DocumentsWindow : Window, INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly CancellationToken _cancellationToken = new CancellationToken();

        private string? _newTitle;
        private string? _newText;
        private string? _statusMessage;

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

        public DocumentsWindow()
        {
            InitializeComponent();
            DataContext = this;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5299/")
            };
        }

        private async void DocumentsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDocumentsAsync();
            await LoadTopicsAsync();
        }

        private async Task LoadTopicsAsync()
        {
            try
            {
                var topics = await _httpClient.GetFromJsonAsync<List<Topic>>("api/documents/topics", _cancellationToken);
                AvailableTopics.Clear();
                foreach (var topic in topics ?? Enumerable.Empty<Topic>())
                {
                    AvailableTopics.Add(new TopicSelectionItem { Id = topic.Id, Name = topic.Name });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading topics: {ex.Message}";
            }
        }

        private async Task LoadDocumentsAsync()
        {
            try
            {
                var documents = await _httpClient.GetFromJsonAsync<List<Document>>("api/documents", _cancellationToken);
                Documents.Clear();
                foreach (var document in documents ?? Enumerable.Empty<Document>())
                {
                    Documents.Add(new DocumentDisplayModel
                    {
                        Id = document.Id,
                        Title = document.Title,
                        Topics = document.Topics
                    });
                }

                StatusMessage = $"{Documents.Count} document(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading documents: {ex.Message}";
            }
        }

        private async void AddDocument_Click(object sender, RoutedEventArgs e)
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

            try
            {
                var dto = new IngestTextRequestDto
                {
                    Title = title,
                    Text = text,
                    Topics = topics
                };

                using var response = await _httpClient.PostAsync(
                    "api/documents",
                    new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json"),
                    _cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(_cancellationToken);
                    MessageBox.Show(error, "Add Document Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                NewTitle = string.Empty;
                NewText = string.Empty;
                foreach (var topic in AvailableTopics)
                {
                    topic.IsSelected = false;
                }
                await LoadDocumentsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Add Document Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteDocument_Click(object sender, RoutedEventArgs e)
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

            try
            {
                using var response = await _httpClient.DeleteAsync($"api/documents/{document.Id}", _cancellationToken);
                response.EnsureSuccessStatusCode();
                Documents.Remove(document);
                StatusMessage = $"Deleted '{document.Title}'.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Delete Document Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
