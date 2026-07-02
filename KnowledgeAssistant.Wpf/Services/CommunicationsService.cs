using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Wpf.Messages;
using KnowledgeAssistant.Wpf.Messages.Conversations;
using MessageServices;
using MessageServices.Enums;
using MessageServices.Messages;
using Serilog.Core;
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
            _messageService.SubscribeAsync<CreateConversationsRequest>(this, CreateConversationsReceived);
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
                        var chunk = JsonSerializer.Deserialize<ChatResponseChunkDto>(data, jsonOptions);
                        switch (currentEvent)
                        {
                            case "conversation-updated": conversationId = chunk?.ConversationId; break;
                            case "token": _messageService.Publish(new ChunkReceivedEvent(chunk?.Content ?? string.Empty)); break;
                            case "done": _messageService.Publish(new ChatCompletedEvent(conversationId)); break;
                        }
                    }
                }
            }
        }
    }
}