using KnowledgeAssistant.Contracts.Tools;
using KnowledgeAssistant.Wpf.Messages.ToolsManagement;
using MessageServices;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace KnowledgeAssistant.Wpf.Views;

public partial class ToolsManagerWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
{
    private readonly MessageService _messageService;

    private ToolDto? _selectedTool;
    private string _toolName = string.Empty;
    private string _description = string.Empty;
    private string _parametersJsonSchema = "{\n  \"type\": \"object\",\n  \"properties\": {}\n}";
    private bool _isEnabled = true;
    private string? _endpointUrl;
    private string _httpMethod = "GET";
    private string? _authLoginUrl;
    private string? _authUsername;
    private string? _authPassword;
    private string _formHeaderText = "Add Tool";
    private string? _errorMessage;

    public ToolsManagerWindow(MessageService messageService)
    {
        InitializeComponent();
        DataContext = this;

        _messageService = messageService;
        _messageService.Subscribe<ToolCreatedEvent>(this, ToolCreatedEventReceived);
        _messageService.Subscribe<ToolUpdatedEvent>(this, ToolUpdatedEventReceived);
        _messageService.Subscribe<ToolDeletedEvent>(this, ToolDeletedEventReceived);
    }

    public ObservableCollection<ToolDto> Tools { get; } = new();

    public ToolDto? SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (SetField(ref _selectedTool, value))
                LoadIntoForm(value);
        }
    }

    public string ToolName
    {
        get => _toolName;
        set => SetField(ref _toolName, value);
    }

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public string ParametersJsonSchema
    {
        get => _parametersJsonSchema;
        set => SetField(ref _parametersJsonSchema, value);
    }

    public new bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    public string? EndpointUrl
    {
        get => _endpointUrl;
        set => SetField(ref _endpointUrl, value);
    }

    public string HttpMethod
    {
        get => _httpMethod;
        set => SetField(ref _httpMethod, value);
    }

    public IReadOnlyList<string> HttpMethods { get; } = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" };

    public string? AuthLoginUrl
    {
        get => _authLoginUrl;
        set => SetField(ref _authLoginUrl, value);
    }

    public string? AuthUsername
    {
        get => _authUsername;
        set => SetField(ref _authUsername, value);
    }

    public string? AuthPassword
    {
        get => _authPassword;
        set => SetField(ref _authPassword, value);
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

    private void ToolCreatedEventReceived(MessageBase message)
    {
        Dispatcher.Invoke(() =>
        {
            ClearForm();
            _messageService?.Publish(new GetToolsRequest());
        });
    }

    private void ToolUpdatedEventReceived(MessageBase message)
    {
        Dispatcher.Invoke(() =>
        {
            ClearForm();
            _messageService?.Publish(new GetToolsRequest());
        });
    }

    private void ToolDeletedEventReceived(MessageBase message)
    {
        Dispatcher.Invoke(() =>
        {
            ClearForm();
            _messageService?.Publish(new GetToolsRequest());
        });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorMessage = null;

        var name = ToolName.Trim();
        var description = Description.Trim();
        var schema = ParametersJsonSchema.Trim();
        var endpointUrl = string.IsNullOrWhiteSpace(EndpointUrl) ? null : EndpointUrl.Trim();
        var httpMethod = string.IsNullOrWhiteSpace(HttpMethod) ? "GET" : HttpMethod.Trim().ToUpperInvariant();
        var authLoginUrl = string.IsNullOrWhiteSpace(AuthLoginUrl) ? null : AuthLoginUrl.Trim();
        var authUsername = string.IsNullOrWhiteSpace(AuthUsername) ? null : AuthUsername.Trim();
        var authPassword = string.IsNullOrWhiteSpace(AuthPassword) ? null : AuthPassword;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(schema))
        {
            ErrorMessage = "Name, Description and Parameters JSON Schema are required.";
            return;
        }

        try
        {
            System.Text.Json.JsonDocument.Parse(schema);
        }
        catch (System.Text.Json.JsonException ex)
        {
            ErrorMessage = $"Parameters JSON Schema is not valid JSON: {ex.Message}";
            return;
        }

        if (SelectedTool == null)
        {
            _messageService.Publish(new CreateToolRequest(name, description, schema, IsEnabled, endpointUrl, httpMethod, authLoginUrl, authUsername, authPassword));
        }
        else
        {
            _messageService.Publish(new UpdateToolRequest(SelectedTool.Id, name, description, schema, IsEnabled, endpointUrl, httpMethod, authLoginUrl, authUsername, authPassword));
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTool == null) return;

        ErrorMessage = null;
        _messageService.Publish(new DeleteToolRequest(SelectedTool.Id));
    }

    private void NewButton_Click(object sender, RoutedEventArgs e) => ClearForm();

    private void ClearForm()
    {
        _selectedTool = null;
        OnPropertyChanged(nameof(SelectedTool));
        ToolName = string.Empty;
        Description = string.Empty;
        ParametersJsonSchema = "{\n  \"type\": \"object\",\n  \"properties\": {}\n}";
        IsEnabled = true;
        EndpointUrl = null;
        HttpMethod = "GET";
        AuthLoginUrl = null;
        AuthUsername = null;
        AuthPassword = null;
        FormHeaderText = "Add Tool";
        ErrorMessage = null;
    }

    private void LoadIntoForm(ToolDto? tool)
    {
        ToolName = tool?.Name ?? string.Empty;
        Description = tool?.Description ?? string.Empty;
        ParametersJsonSchema = tool?.ParametersJsonSchema ?? "{\n  \"type\": \"object\",\n  \"properties\": {}\n}";
        IsEnabled = tool?.IsEnabled ?? true;
        FormHeaderText = tool == null ? "Add Tool" : "Edit Tool";
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

    private async void ToolsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var result = (await _messageService.RequestAsync<ToolsReceivedEvent>(new GetToolsRequest())).FirstOrDefault();
        if (result != null && result.ErrorMessage == null)
        {
            Dispatcher.Invoke(() =>
            {
                Tools.Clear();
                foreach (var tool in result.Tools.OrderBy(t => t.Name))
                {
                    Tools.Add(tool);
                }
            });
        }
    }
}
