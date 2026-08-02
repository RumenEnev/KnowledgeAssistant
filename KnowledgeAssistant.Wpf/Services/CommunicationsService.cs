using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Contracts.Repositories;
using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Domain.Documents;
using KnowledgeAssistant.Wpf.Messages;
using KnowledgeAssistant.Wpf.Messages.Conversations;
using KnowledgeAssistant.Wpf.Messages.Documentation;
using KnowledgeAssistant.Wpf.Messages.Documents;
using KnowledgeAssistant.Wpf.Messages.Files;
using KnowledgeAssistant.Wpf.Messages.ModelContextWindows;
using KnowledgeAssistant.Wpf.Messages.RepositoriesManagement;
using KnowledgeAssistant.Wpf.Models;
using MessageServices;
using MessageServices.Enums;
using MessageServices.Messages;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace KnowledgeAssistant.Wpf.Services
{
    public class CommunicationsService : IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;
        private readonly CancellationToken _cancellationToken = new CancellationToken();
        private HttpClient _httpClient;

        public CommunicationsService(MessageService messageService, IConfiguration configuration)
        {
            _messageService = messageService;
            _httpClient = new HttpClient();

            var baseUrl = configuration["Api:BaseUrl"] ?? "http://localhost:5299/";
            _httpClient.BaseAddress = new Uri(baseUrl);

            if (File.Exists("Settings.json"))
            {
                var settings = File.ReadAllText("Settings.json");
                var appConfig = JsonSerializer.Deserialize<ApplicationConfiguration>(settings);
                if (appConfig != null && !string.IsNullOrWhiteSpace(appConfig.BaseUrl))
                {
                    _httpClient = new HttpClient();
                    _httpClient.BaseAddress = new Uri(appConfig.BaseUrl);
                }
            }

            _messageService.Subscribe<GenerateTitleRequest>(this, GenerateTitleReceived);
            _messageService.Subscribe<GetAvailableModelsRequest>(this, GetAvailableModelsReceived);
            _messageService.Subscribe<GetConversationsRequest>(this, GetConversationsReceived);
            _messageService.Subscribe<GetConversationRequest>(this, GetConversationReceived);
            _messageService.Subscribe<RefreshConversationRequest>(this, RefreshConversationReceived);
            _messageService.Subscribe<UpdateConversationTitleRequest>(this, UpdateConversationTitleReceived);
            _messageService.Subscribe<UpdateConversationTopicRequest>(this, UpdateConversationTopicReceived);
            _messageService.SubscribeAsync<CreateConversationsRequest>(this, CreateConversationsReceived);
            _messageService.Subscribe<DeleteConversationRequest>(this, DeleteConversationReceived);
            _messageService.Subscribe<UpdateSelectedModelRequest>(this, UpdateSelectedModelReceived);
            _messageService.Subscribe<GetSelectedModelRequest>(this, GetSelectedModelReceived);
            _messageService.Subscribe<GetDocumentsRequest>(this, GetDocumentsReceived);
            _messageService.Subscribe<GetTopicsRequest>(this, GetTopicsReceived);
            _messageService.Subscribe<CreateTopicRequest>(this, CreateTopicReceived);
            _messageService.Subscribe<UpdateTopicRequest>(this, UpdateTopicReceived);
            _messageService.Subscribe<DeleteTopicRequest>(this, DeleteTopicReceived);
            _messageService.Subscribe<AddDocumentRequest>(this, AddDocumentReceived);
            _messageService.Subscribe<UpdateDocumentRequest>(this, UpdateDocumentReceived);
            _messageService.Subscribe<DeleteDocumentRequest>(this, DeleteDocumentReceived);
            _messageService.Subscribe<GetChunkingSettingsRequest>(this, GetChunkingSettingsReceived);
            _messageService.Subscribe<UpdateChunkingSettingsRequest>(this, UpdateChunkingSettingsReceived);
            _messageService.Subscribe<GetModelContextWindowsRequest>(this, GetModelContextWindowsReceived);
            _messageService.Subscribe<UpdateApiUrlRequest>(this, UpdateApiUrlReceived);
            _messageService.Subscribe<CreateRepositoryRequest>(this, CreateRepositoryReceived);
            _messageService.Subscribe<UpdateRepositoryRequest>(this, UpdateRepositoryReceived);
            _messageService.Subscribe<DeleteRepositoryRequest>(this, DeleteRepositoryReceived);
            _messageService.Subscribe<SaveDocumentationRequest>(this, SaveDocumentationReceived);
            _messageService.Subscribe<SendPromptRequest>(this, SendPromptReceived);

            _messageService.SubscribeAsync<GetRepositoriesRequest>(this, GetRepositoriesReceived);
        }

        private void UpdateApiUrlReceived(MessageBase message)
        {
            if (message is UpdateApiUrlRequest request)
            {
                _httpClient = new HttpClient();
                _httpClient.BaseAddress = new Uri(request.Url);
                File.WriteAllText($"Settings.json", JsonSerializer.Serialize(new ApplicationConfiguration()
                {
                    BaseUrl = request.Url
                }));

                _messageService.Publish(new UserMessage("Info", $"API URL updated to: {request.Url}", MessageType.ShortInfo));
            }
        }

        private async void GetDocumentsReceived(MessageBase message)
        {
            if (message is GetDocumentsRequest)
            {
                await LoadDocumentsAsync();
            }
        }

        private async Task LoadDocumentsAsync()
        {
            try
            {
                var documents = await _httpClient.GetFromJsonAsync<List<Document>>("api/documents", _cancellationToken);
                _messageService.Publish(new DocumentsUpdatedEvent(documents ?? Enumerable.Empty<Document>()));
            }
            catch (Exception ex)
            {
                _messageService.Publish(new UserMessage("Error", $"Error fetching documents: {ex.Message}", MessageType.Error));
            }
        }

        private async void GetTopicsReceived(MessageBase message)
        {
            if (message is GetTopicsRequest)
            {
                await LoadTopicsAsync();
            }
        }

        private async Task LoadTopicsAsync()
        {
            try
            {
                var topics = await _httpClient.GetFromJsonAsync<List<Topic>>("api/topics", _cancellationToken);
                _messageService.Publish(new TopicsUpdatedEvent(topics ?? Enumerable.Empty<Topic>()));
            }
            catch (Exception ex)
            {
                _messageService.Publish(new UserMessage("Error", $"Error fetching topics: {ex.Message}", MessageType.Error));
            }
        }

        private async void CreateTopicReceived(MessageBase message)
        {
            if (message is CreateTopicRequest request)
            {
                try
                {
                    var dto = new TopicRequestDto { Name = request.Name };
                    var response = await _httpClient.PostAsJsonAsync("api/topics", dto, _cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync(_cancellationToken);
                        _messageService.Publish(new UserMessage("Add Topic Failed", error, MessageType.Error));
                        return;
                    }

                    await LoadTopicsAsync();
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Add Topic Failed", $"Error creating topic: {ex.Message}", MessageType.Error));
                }
            }
        }

        private async void UpdateTopicReceived(MessageBase message)
        {
            if (message is UpdateTopicRequest request)
            {
                try
                {
                    var dto = new TopicRequestDto { Name = request.Name };
                    var response = await _httpClient.PutAsJsonAsync($"api/topics/{request.TopicId}", dto, _cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync(_cancellationToken);
                        _messageService.Publish(new UserMessage("Update Topic Failed", error, MessageType.Error));
                        return;
                    }

                    await LoadTopicsAsync();
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Update Topic Failed", $"Error updating topic: {ex.Message}", MessageType.Error));
                }
            }
        }

        private async void DeleteTopicReceived(MessageBase message)
        {
            if (message is DeleteTopicRequest request)
            {
                try
                {
                    var response = await _httpClient.DeleteAsync($"api/topics/{request.TopicId}", _cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync(_cancellationToken);
                        _messageService.Publish(new UserMessage("Delete Topic Failed", error, MessageType.Error));
                        return;
                    }

                    await LoadTopicsAsync();
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Delete Topic Failed", $"Error deleting topic: {ex.Message}", MessageType.Error));
                }
            }
        }

        private async void AddDocumentReceived(MessageBase message)
        {
            if (message is AddDocumentRequest request)
            {
                try
                {
                    var dto = new IngestTextRequestDto
                    {
                        Title = request.Title,
                        Text = request.Text,
                        DocumentType = request.DocumentType,
                        Topics = request.Topics.ToList()
                    };

                    using var response = await _httpClient.PostAsync("api/documents",
                        new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json"),
                        _cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync(_cancellationToken);
                        _messageService.Publish(new UserMessage("Add Document Failed", error, MessageType.Error));
                        return;
                    }

                    var result = await response.Content.ReadFromJsonAsync<AddDocumentResultDto>(_cancellationToken);
                    _messageService.Publish(new DocumentAddedEvent(result?.DocumentId ?? 0));
                    await LoadDocumentsAsync();
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Add Document Failed", ex.Message, MessageType.Error));
                }
            }
        }

        private async void UpdateDocumentReceived(MessageBase message)
        {
            if (message is UpdateDocumentRequest request)
            {
                try
                {
                    var dto = new IngestTextRequestDto
                    {
                        Title = request.Title,
                        Text = request.Text,
                        DocumentType = request.DocumentType,
                        Topics = request.Topics.ToList()
                    };

                    using var response = await _httpClient.PutAsync($"api/documents/{request.DocumentId}",
                        new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json"),
                        _cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync(_cancellationToken);
                        _messageService.Publish(new UserMessage("Update Document Failed", error, MessageType.Error));
                        return;
                    }

                    _messageService.Publish(new DocumentUpdatedEvent(request.DocumentId));
                    await LoadDocumentsAsync();
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Update Document Failed", ex.Message, MessageType.Error));
                }
            }
        }

        private async void DeleteDocumentReceived(MessageBase message)
        {
            if (message is DeleteDocumentRequest request)
            {
                try
                {
                    using var response = await _httpClient.DeleteAsync($"api/documents/{request.DocumentId}", _cancellationToken);
                    response.EnsureSuccessStatusCode();
                    _messageService.Publish(new DocumentDeletedEvent(request.DocumentId));
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Delete Document Failed", ex.Message, MessageType.Error));
                }
            }
        }

        private async void GetChunkingSettingsReceived(MessageBase message)
        {
            if (message is GetChunkingSettingsRequest)
            {
                try
                {
                    var dto = await _httpClient.GetFromJsonAsync<ChunkingSettingsDto>("api/configuration/chunking-settings", _cancellationToken);
                    _messageService.Publish(new ChunkingSettingsUpdatedEvent(dto?.ChunkTargetSizeChars ?? 1000, dto?.ChunkOverlapChars ?? 150));
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error fetching chunking settings: {ex.Message}", MessageType.Error));
                }
            }
        }

        private async void UpdateChunkingSettingsReceived(MessageBase message)
        {
            if (message is UpdateChunkingSettingsRequest request)
            {
                try
                {
                    var dto = new ChunkingSettingsDto
                    {
                        ChunkTargetSizeChars = request.ChunkTargetSizeChars,
                        ChunkOverlapChars = request.ChunkOverlapChars
                    };

                    using var response = await _httpClient.PutAsync(
                        "api/configuration/chunking-settings",
                        new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json"),
                        _cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync(_cancellationToken);
                        _messageService.Publish(new UserMessage("Save Chunking Settings Failed", error, MessageType.Error));
                        return;
                    }

                    _messageService.Publish(new ChunkingSettingsUpdatedEvent(request.ChunkTargetSizeChars, request.ChunkOverlapChars));
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Save Chunking Settings Failed", ex.Message, MessageType.Error));
                }
            }
        }

        private async void GetModelContextWindowsReceived(MessageBase message)
        {
            if (message is GetModelContextWindowsRequest)
            {
                try
                {
                    var dtos = await _httpClient.GetFromJsonAsync<List<ModelContextWindowDto>>("api/models/context-windows", _cancellationToken);
                    var models = (dtos ?? new List<ModelContextWindowDto>())
                        .Select(d => new ModelContextWindowInfo(d.Id, d.Name, d.Size, d.ContextLength, d.Family, d.QuantizationLevel, d.ParameterSize))
                        .ToList();
                    _messageService.Publish(new ModelContextWindowsUpdatedEvent(models));
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error fetching model context windows: {ex.Message}", MessageType.Error));
                }
            }
        }

        private async void GetSelectedModelReceived(MessageBase message)
        {
            if (message is GetSelectedModelRequest)
            {
                try
                {
                    var dto = await _httpClient.GetFromJsonAsync<SelectedModelDto>("api/configuration/selected-model", _cancellationToken);
                    _messageService.Publish(new SelectedModelUpdatedEvent(dto?.SelectedModel));
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error fetching selected model: {ex.Message}", MessageType.Error));
                }
            }
        }

        private async void UpdateSelectedModelReceived(MessageBase message)
        {
            if (message is UpdateSelectedModelRequest request)
            {
                try
                {
                    var httpRequest = new HttpRequestMessage(HttpMethod.Put, "api/configuration/selected-model");
                    var dto = new UpdateSelectedModelDto { SelectedModel = request.SelectedModel };
                    httpRequest.Content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

                    using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error saving selected model: {ex.Message}", MessageType.Error));
                }
            }
        }

        private async void DeleteConversationReceived(MessageBase message)
        {
            if (message is DeleteConversationRequest request)
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/conversations/{request.ConversationId}");
                var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
                response.EnsureSuccessStatusCode();
                var deletedConversationId = request.ConversationId;
                if (deletedConversationId != Guid.Empty)
                {
                    _messageService.Publish(new ConversationDeletedEvent(deletedConversationId));
                }
            }
        }

        private async void UpdateConversationTitleReceived(MessageBase message)
        {
            if (message is UpdateConversationTitleRequest request)
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"api/conversations/{request.ConversationId}/title?newTitle={Uri.EscapeDataString(request.NewTitle)}");
                var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
                response.EnsureSuccessStatusCode();

                var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>(_cancellationToken);
                if (conversation != null)
                {
                    _messageService.Publish(new ConversationUpdatedEvent(new Conversation
                    {
                        Id = conversation.Id,
                        Title = conversation.Title
                    }));
                }
            }
        }

        private async void UpdateConversationTopicReceived(MessageBase message)
        {
            if (message is UpdateConversationTopicRequest request)
            {
                var query = request.TopicId.HasValue ? $"?topicId={request.TopicId}" : string.Empty;
                var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"api/conversations/{request.ConversationId}/topic{query}");
                try
                {
                    var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>(_cancellationToken);
                    if (conversation != null)
                    {
                        _messageService.Publish(new ConversationUpdatedEvent(new Conversation
                        {
                            Id = conversation.Id,
                            Title = conversation.Title,
                            TopicId = conversation.TopicId,
                            Topic = conversation.Topic
                        }));
                    }
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error setting the conversation topic: {ex.Message}", MessageType.Error));
                }
            }
        }

        private async void GetConversationReceived(MessageBase message)
        {
            if (message is GetConversationRequest request)
            {
                try
                {
                    var conversation = await _httpClient.GetFromJsonAsync<ConversationDto>($"api/conversations/{request.ConversationId}", _cancellationToken);
                    if (conversation != null)
                    {
                        _messageService.Publish(new ConversationLoadedEvent(conversation));
                    }
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error fetching conversation: {ex.Message}", MessageType.Information));
                }
            }
        }

        private async void RefreshConversationReceived(MessageBase message)
        {
            if (message is RefreshConversationRequest request)
            {
                try
                {
                    var conversation = await _httpClient.GetFromJsonAsync<ConversationDto>($"api/conversations/{request.ConversationId}", _cancellationToken);
                    if (conversation != null)
                    {
                        _messageService.Publish(new ConversationUpdatedEvent(new Conversation
                        {
                            Id = conversation.Id,
                            Title = conversation.Title,
                            TopicId = conversation.TopicId,
                            Topic = conversation.Topic
                        }));
                    }
                }
                catch
                {
                    // Best-effort refresh; ignore failures so the chat flow is not disrupted.
                }
            }
        }

        private async void GetConversationsReceived(MessageBase message)
        {
            if (message is GetConversationsRequest)
            {
                try
                {
                    var conversations = await _httpClient.GetFromJsonAsync<List<ConversationDto>>("api/conversations", _cancellationToken);
                    _messageService.Publish(new ConversationsUpdatedEvent(conversations?.Select(c => new Conversation
                    {
                        Id = c.Id,
                        Title = c.Title,
                        TopicId = c.TopicId,
                        Topic = c.Topic
                    }) ?? Enumerable.Empty<Conversation>()));
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error fetching conversations: {ex.Message}", MessageType.Information));
                }
            }
        }

        private async Task<MessageBase> CreateConversationsReceived(MessageBase message)
        {
            if (message is CreateConversationsRequest)
            {
                try
                {
                    var response = await _httpClient.PostAsync("api/conversations", null, _cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>(_cancellationToken);
                    return new CreateConversationsResponse(conversation ?? new ConversationDto() { Id = Guid.Empty });
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error creating a new conversation: {ex.Message}", MessageType.Information));
                }
            }

            return new CreateConversationsResponse(new ConversationDto() { Id = Guid.Empty });
        }

        private async void GetAvailableModelsReceived(MessageBase message)
        {
            if (message is GetAvailableModelsRequest)
            {
                try
                {
                    var models = await _httpClient.GetFromJsonAsync<List<ModelInfoDto>>("api/models");
                    _messageService.Publish(new AvailableModelsUpdatedEvent(models?.Select(model => model.Name) ?? Enumerable.Empty<string>()));
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error fetching available models: {ex.Message}", MessageType.Information));
                }
            }
        }

        private async void GenerateTitleReceived(MessageBase message)
        {
            if (message is GenerateTitleRequest request)
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat/title");
                var dto = new ChatRequestDto
                {
                    Message = request.UserPrompt,
                    Model = request.Model,
                };

                httpRequest.Content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
                using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseDto = await response.Content.ReadFromJsonAsync<ConversationDto>(_cancellationToken);
                if (!string.IsNullOrWhiteSpace(responseDto?.Title))
                {
                    await UpdateTitle(responseDto.Title, request.ConversationId);
                }
                ;

                _messageService.Publish(new TitleGeneratedEvent(responseDto?.Title ?? string.Empty, request.ConversationId));
            }
        }

        private async Task UpdateTitle(string newTitle, Guid conversationId)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"api/conversations/{conversationId}/title?newTitle={Uri.EscapeDataString(newTitle)}");
            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async void SendPromptReceived(MessageBase message)
        {
            if (message is SendPromptRequest request)
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat");
                var dto = new ChatRequestDto
                {
                    Message = request.Prompt,
                    Model = request.Model,
                    ConversationId = request.ConversationId
                };

                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                httpRequest.Content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
                try
                {
                    using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
                    response.EnsureSuccessStatusCode();

                    await using var stream = await response.Content.ReadAsStreamAsync(_cancellationToken);
                    using var reader = new StreamReader(stream);
                    string? currentEvent = null;
                    Guid? conversationId = null;
                    while (!reader.EndOfStream && !_cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        if (line.StartsWith("event: "))
                        {
                            currentEvent = line["event: ".Length..];
                            continue;
                        }

                        if (line.StartsWith("data: "))
                        {
                            var data = line["data: ".Length..];
                            ChatResponseChunkDto? chunk;
                            switch (currentEvent)
                            {
                                case SseEvents.ConversationUpdated:
                                    chunk = JsonSerializer.Deserialize<ChatResponseChunkDto>(data, jsonOptions);
                                    conversationId = chunk?.ConversationId;
                                    break;
                                case SseEvents.Token:
                                    chunk = JsonSerializer.Deserialize<ChatResponseChunkDto>(data, jsonOptions);
                                    _messageService.Publish(new ChunkReceivedEvent(chunk?.Content ?? string.Empty));
                                    break;
                                case SseEvents.Done:
                                    var metadata = JsonSerializer.Deserialize<MessageDoneDto>(data, jsonOptions);
                                    _messageService.Publish(new ChatCompletedEvent(metadata?.PromptTokens ?? 0, metadata?.ResponseTokens ?? 0));
                                    break;
                                case SseEvents.Error:
                                    var error = JsonSerializer.Deserialize<ErrorEventDto>(data, jsonOptions);
                                    _messageService.Publish(new UserMessage("Error", error?.Message ?? "An error occurred while generating the response.", MessageType.Error));
                                    _messageService.Publish(new ChatCompletedEvent(0, 0));
                                    break;
                                case SseEvents.DocumentationForFile:
                                    var fileName = JsonSerializer.Deserialize<ProgressEventDto>(data, jsonOptions)?.Message;
                                    if (fileName != null)
                                    {
                                        var repositories = await _httpClient.GetFromJsonAsync<List<RepositoryDto>>("api/repositories", _cancellationToken);
                                        if (repositories?.Any() == true)
                                        {
                                            _messageService.Publish(new CreateFileDocumentationRequest(fileName, repositories.Select(r => r.RootPath).ToList()));
                                        }
                                    }
                                    break;
                                case SseEvents.Progress:
                                    var progress = JsonSerializer.Deserialize<ProgressEventDto>(data, jsonOptions);
                                    if (!string.IsNullOrWhiteSpace(progress?.Message))
                                    {
                                        _messageService.Publish(new UserMessage("Info", progress.Message, MessageType.ShortInfo));
                                    }
                                    break;
                                    //    case SseEvents.MessageCompleted: _messageService.Publish(new ChatCompletedEvent(conversationId)); break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error sending message: {ex.Message}", MessageType.Error));
                    _messageService.Publish(new ChatCompletedEvent(0, 0));
                }
            }
        }

        private async Task<MessageBase> GetRepositoriesReceived(MessageBase message)
        {
            if (message is GetRepositoriesRequest)
            {
                try
                {
                    var repositories = await _httpClient.GetFromJsonAsync<List<RepositoryDto>>("api/repositories", _cancellationToken);
                    return new RepositoriesReceivedEvent(repositories ?? Enumerable.Empty<RepositoryDto>());
                }
                catch (Exception ex)
                {
                    return new RepositoriesReceivedEvent(Enumerable.Empty<RepositoryDto>(), $"Error fetching repositories: {ex.Message}");
                }
            }

            return new RepositoriesReceivedEvent(Enumerable.Empty<RepositoryDto>(), $"Wrong message type");
        }

        private async void CreateRepositoryReceived(MessageBase message)
        {
            if (message is CreateRepositoryRequest request)
            {
                try
                {
                    var dto = new CreateRepositoryDto(request.Name, request.RootPath, request.Description);
                    using var response = await _httpClient.PostAsJsonAsync("api/repositories", dto, _cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await ReadErrorMessageAsync(response);
                        return;
                    }

                    var created = await response.Content.ReadFromJsonAsync<RepositoryDto>(cancellationToken: _cancellationToken);
                    if (created != null)
                    {
                        _messageService.Publish(new RepositoryCreatedEvent(created));
                    }
                }
                catch (Exception ex)
                {
                    // _messageService.Publish(new RepositoryOperationFailedEvent("Create", $"Error creating repository: {ex.Message}"));
                }
            }
        }

        private async void UpdateRepositoryReceived(MessageBase message)
        {
            if (message is UpdateRepositoryRequest request)
            {
                try
                {
                    var dto = new UpdateRepositoryDto(request.Name, request.RootPath, request.Description);
                    using var response = await _httpClient.PutAsJsonAsync($"api/repositories/{request.Id}", dto, _cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await ReadErrorMessageAsync(response);
                        //    _messageService.Publish(new RepositoryOperationFailedEvent("Update", error));
                        return;
                    }

                    _messageService.Publish(new RepositoryUpdatedEvent(request.Id));
                }
                catch (Exception ex)
                {
                    // _messageService.Publish(new RepositoryOperationFailedEvent("Update", $"Error updating repository: {ex.Message}"));
                }
            }
        }

        private async void DeleteRepositoryReceived(MessageBase message)
        {
            if (message is DeleteRepositoryRequest request)
            {
                try
                {
                    using var response = await _httpClient.DeleteAsync($"api/repositories/{request.Id}", _cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await ReadErrorMessageAsync(response);
                        // _messageService.Publish(new RepositoryOperationFailedEvent("Delete", error));
                        return;
                    }

                    _messageService.Publish(new RepositoryDeletedEvent(request.Id));
                }
                catch (Exception ex)
                {
                    // _messageService.Publish(new RepositoryOperationFailedEvent("Delete", $"Error deleting repository: {ex.Message}"));
                }
            }
        }

        private async void SaveDocumentationReceived(MessageBase message)
        {
            if (message is SaveDocumentationRequest request)
            {
                try
                {
                    _messageService.Publish(new UserMessage("Info", $"Saving documentation for '{request.RelativeFilePath}' and ingesting it into the RAG index...", MessageType.ShortInfo));

                    var dto = new SaveDocumentationRequestDto
                    {
                        RepositoryId = request.RepositoryId,
                        RelativeFilePath = request.RelativeFilePath,
                        Title = request.Title,
                        Markdown = request.Markdown
                    };

                    using var response = await _httpClient.PostAsJsonAsync("api/documentation/save", dto, _cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await ReadErrorMessageAsync(response);
                        _messageService.Publish(new UserMessage("Info", $"Failed to save documentation: {error}", MessageType.ShortInfo));
                        _messageService.Publish(new DocumentationSaveFailedEvent(request.CorrelationId, error));
                        return;
                    }

                    var result = await response.Content.ReadFromJsonAsync<SaveDocumentationResultDto>(_cancellationToken);
                    _messageService.Publish(new UserMessage("Info", $"Documentation saved to {result?.SavedFilePath} and ingested into the RAG index.", MessageType.ShortInfo));
                    _messageService.Publish(new DocumentationSavedEvent(request.CorrelationId, result?.DocumentId ?? 0, result?.SavedFilePath ?? string.Empty));
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Info", $"Error saving documentation: {ex.Message}", MessageType.ShortInfo));
                    _messageService.Publish(new DocumentationSaveFailedEvent(request.CorrelationId, $"Error saving documentation: {ex.Message}"));
                }
            }
        }

        private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return $"Request failed with status code {(int)response.StatusCode}.";
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("error", out var errorProperty))
                {
                    return errorProperty.GetString() ?? raw;
                }
            }
            catch (JsonException)
            {
                // Not a JSON error payload - fall back to the raw content below.
            }

            return raw;
        }
    }
}