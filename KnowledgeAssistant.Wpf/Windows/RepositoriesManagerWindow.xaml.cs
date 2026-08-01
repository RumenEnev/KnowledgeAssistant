using KnowledgeAssistant.Contracts.Repositories;
using KnowledgeAssistant.Wpf.Messages.RepositoriesManagement;
using MessageServices;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace KnowledgeAssistant.Wpf.Views;

public partial class RepositoriesManagerWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
{
    private readonly MessageService _messageService;

    private RepositoryDto? _selectedRepository;
    private string _repositoryName = string.Empty;
    private string _rootPath = string.Empty;
    private string? _description;
    private string _formHeaderText = "Add Repository";
    private string? _errorMessage;

    public RepositoriesManagerWindow(MessageService messageService)
    {
        InitializeComponent();
        DataContext = this;

        _messageService = messageService;
        _messageService.Subscribe<RepositoryCreatedEvent>(this, RepositoryCreatedEventReceived);
        _messageService.Subscribe<RepositoryUpdatedEvent>(this, RepositoryUpdatedEventReceived);
        _messageService.Subscribe<RepositoryDeletedEvent>(this, RepositoryDeletedEventReceived);
    }

    public ObservableCollection<RepositoryDto> Repositories { get; } = new();

    public RepositoryDto? SelectedRepository
    {
        get => _selectedRepository;
        set
        {
            if (SetField(ref _selectedRepository, value))
                LoadIntoForm(value);
        }
    }

    public string RepositoryName
    {
        get => _repositoryName;
        set => SetField(ref _repositoryName, value);
    }

    public string RootPath
    {
        get => _rootPath;
        set => SetField(ref _rootPath, value);
    }

    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public string FormHeaderText
    {
        get => _formHeaderText;
        set => SetField(ref _formHeaderText, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetField(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private void RepositoryCreatedEventReceived(MessageBase message)
    {
        Dispatcher.Invoke(() =>
        {
            ClearForm();
            _messageService?.Publish(new GetRepositoriesRequest());
        });
    }

    private void RepositoryUpdatedEventReceived(MessageBase message)
    {
        Dispatcher.Invoke(() =>
        {
            ClearForm();
            _messageService?.Publish(new GetRepositoriesRequest());
        });
    }

    private void RepositoryDeletedEventReceived(MessageBase message)
    {
        Dispatcher.Invoke(() =>
        {
            ClearForm();
            _messageService?.Publish(new GetRepositoriesRequest());
        });
    }

    private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select repository root folder" };
        if (dialog.ShowDialog() == true)
            RootPath = dialog.FolderName;
    }

    private void SaveButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ErrorMessage = null;

        var name = RepositoryName.Trim();
        var rootPath = RootPath.Trim();
        var description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rootPath))
        {
            ErrorMessage = "Name and Root Path are required.";
            return;
        }

        if (SelectedRepository == null)
        {
            _messageService.Publish(new CreateRepositoryRequest(name, rootPath, description));
        }
        else
        {
            _messageService.Publish(new UpdateRepositoryRequest(SelectedRepository.Id, name, rootPath, description));
        }
    }

    private void DeleteButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (SelectedRepository == null) return;

        ErrorMessage = null;
        _messageService.Publish(new DeleteRepositoryRequest(SelectedRepository.Id));
    }

    private void NewButton_Click(object sender, System.Windows.RoutedEventArgs e) => ClearForm();

    private void ClearForm()
    {
        _selectedRepository = null;
        OnPropertyChanged(nameof(SelectedRepository));
        RepositoryName = string.Empty;
        RootPath = string.Empty;
        Description = null;
        FormHeaderText = "Add Repository";
        ErrorMessage = null;
    }

    private void LoadIntoForm(RepositoryDto? repo)
    {
        RepositoryName = repo?.Name ?? string.Empty;
        RootPath = repo?.RootPath ?? string.Empty;
        Description = repo?.Description;
        FormHeaderText = repo == null ? "Add Repository" : "Edit Repository";
        ErrorMessage = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async void RepositoriesWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var result = (await _messageService.RequestAsync<RepositoriesReceivedEvent>(new GetRepositoriesRequest())).FirstOrDefault();
        if (result != null && result.ErrorMessage == null)
        {
            Dispatcher.Invoke(() =>
            {
                Repositories.Clear();
                foreach (var repo in result.Repositories.OrderBy(r => r.Name))
                {
                    Repositories.Add(repo);
                }
            });
        }
    }
}