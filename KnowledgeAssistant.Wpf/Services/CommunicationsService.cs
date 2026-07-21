using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Domain.Documents;
using KnowledgeAssistant.Wpf.Messages;
using KnowledgeAssistant.Wpf.Messages.Conversations;
using KnowledgeAssistant.Wpf.Messages.Documents;
using MessageServices;
using MessageServices.Enums;
using MessageServices.Messages;
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
        private readonly HttpClient _httpClient;
        private readonly CancellationToken _cancellationToken = new CancellationToken();

        public CommunicationsService(MessageService messageService)
        {
            _messageService = messageService;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("http://localhost:5299/");

            _messageService.Subscribe<GenerateTitleRequest>(this, GenerateTitleReceived);
            _messageService.Subscribe<SendUserMessageRequest>(this, SendPromptReceived);
            _messageService.Subscribe<GetAvailableModelsRequest>(this, GetAvailableModelsReceived);
            _messageService.Subscribe<GetConversationsRequest>(this, GetConversationsReceived);
            _messageService.Subscribe<GetConversationRequest>(this, GetConversationReceived);
            _messageService.Subscribe<UpdateConversationTitleRequest>(this, UpdateConversationTitleReceived);
            _messageService.SubscribeAsync<CreateConversationsRequest>(this, CreateConversationsReceived);
            _messageService.Subscribe<DeleteConversationRequest>(this, DeleteConversationReceived);
            _messageService.Subscribe<UpdateSelectedModelRequest>(this, UpdateSelectedModelReceived);
            _messageService.Subscribe<GetSelectedModelRequest>(this, GetSelectedModelReceived);
            _messageService.Subscribe<GetDocumentsRequest>(this, GetDocumentsReceived);
            _messageService.Subscribe<GetTopicsRequest>(this, GetTopicsReceived);
            _messageService.Subscribe<AddDocumentRequest>(this, AddDocumentReceived);
            _messageService.Subscribe<DeleteDocumentRequest>(this, DeleteDocumentReceived);
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
                try
                {
                    var topics = await _httpClient.GetFromJsonAsync<List<Topic>>("api/documents/topics", _cancellationToken);
                    _messageService.Publish(new TopicsUpdatedEvent(topics ?? Enumerable.Empty<Topic>()));
                }
                catch (Exception ex)
                {
                    _messageService.Publish(new UserMessage("Error", $"Error fetching topics: {ex.Message}", MessageType.Error));
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
                        Topics = request.Topics.ToList()
                    };

                    using var response = await _httpClient.PostAsync(
                        "api/documents",
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
                    var httpRequest = new HttpRequestMessage(HttpMethod.Put, "http://localhost:5299/api/configuration/selected-model");
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
                var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"http://localhost:5299/api/conversations/{request.ConversationId}");
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
                var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"http://localhost:5299/api/conversations/{request.ConversationId}/title?newTitle={Uri.EscapeDataString(request.NewTitle)}");
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
                        Title = c.Title
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
                    var response = await _httpClient.PostAsync("http://localhost:5299/api/conversations", null, _cancellationToken);
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
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5299/api/chat/title");
                var dto = new ChatRequestDto
                {
                    Message = request.UserPrompt,
                    Model = request.Model, //  "mistral-nemo:12b-instruct-2407-q5_K_M"
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
            var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"http://localhost:5299/api/conversations/{conversationId}/title?newTitle={Uri.EscapeDataString(newTitle)}");
            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async void SendPromptReceived(MessageBase message)
        {
            if (message is SendUserMessageRequest request)
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5299/api/chat");
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
    }
}