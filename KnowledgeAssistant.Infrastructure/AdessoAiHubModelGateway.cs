using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Infrastructure.Dto.AiHub;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KnowledgeAssistant.Infrastructure;

public sealed class AdessoAiHubModelGateway : INamedModelGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private int _promptTokensCount;
    private int _responseTokensCount;

    public AdessoAiHubModelGateway(HttpClient httpClient, IOptions<AdessoAiHubOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _apiKey = options.Value.ApiKey;

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException($"Configuration value '{AdessoAiHubOptions.SectionName}:ApiKey' is required.");
        }
    }

    public string Provider => ModelProviderNames.AdessoAiHub;

    public (int, int) GetTokenConsumption() =>
        (Volatile.Read(ref _promptTokensCount), Volatile.Read(ref _responseTokensCount));

    public async Task<string> GenerateAsync(string model, ChatMessage userMessage, ChatMessage systemMessage, CancellationToken cancellationToken)
    {
        ResetTokenConsumption();
        var messages = NormalizeMessageOrder([systemMessage, userMessage]);
        var payload = new
        {
            model,
            messages = messages.Select(message => new
            {
                role = message.Role.ToLowerInvariant(),
                content = message.Content
            }),
            stream = false
        };

        using var request = CreateJsonRequest(HttpMethod.Post, "v1/chat/completions", payload);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, model, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<AiHubChatCompletionResponseDto>(stream, JsonOptions, cancellationToken);

        UpdateTokenConsumption(result?.Usage);
        return result?.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamAsync(string model, List<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ResetTokenConsumption();
        var normalizedMessages = NormalizeMessageOrder(messages);
        var requestBody = new
        {
            model,
            messages = normalizedMessages.Select(message => new
            {
                role = message.Role,
                content = message.Content
            }),
            stream = true,
            // Requests a final SSE chunk containing usage. LiteLLM/OpenAI-compatible
            // gateways normally support this. If a model omits it, token counts stay 0.
            stream_options = new { include_usage = true }
        };

        using var request = CreateJsonRequest(HttpMethod.Post, "v1/chat/completions", requestBody);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, model, cancellationToken);

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line["data:".Length..].Trim();
            if (payload.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            AiHubChatCompletionResponseDto? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<AiHubChatCompletionResponseDto>(payload, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("The AI Hub returned an invalid server-sent event payload.", exception);
            }

            UpdateTokenConsumption(chunk?.Usage);
            var content = chunk?.Choices.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(content))
            {
                yield return content;
            }
        }
    }

    public async Task<ToolChatResult> ChatWithToolsAsync(
        string model,
        ChatMessage userMessage,
        ChatMessage systemMessage,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        ResetTokenConsumption();
        var toolsArray = new JsonArray();
        foreach (var tool in tools)
        {
            JsonNode parameters;
            try
            {
                parameters = JsonNode.Parse(tool.ParametersJsonSchema) ?? throw new JsonException("The schema is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"Tool '{tool.Name}' has an invalid ParametersJsonSchema.", exception);
            }

            toolsArray.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = parameters
                }
            });
        }

        var messages = NormalizeMessageOrder([systemMessage, userMessage]);
        var payload = new
        {
            model,
            messages = messages.Select(message => new
            {
                role = message.Role.ToLowerInvariant(),
                content = message.Content
            }),
            tools = toolsArray,
            tool_choice = "auto",
            temperature = 0,
            stream = false
        };

        using var request = CreateJsonRequest(HttpMethod.Post, "v1/chat/completions", payload);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, model, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<AiHubChatCompletionResponseDto>(stream, JsonOptions, cancellationToken);

        UpdateTokenConsumption(result?.Usage);

        var message = result?.Choices.FirstOrDefault()?.Message;
        var toolCalls = (message?.ToolCalls ?? [])
            .Where(call => !string.IsNullOrWhiteSpace(call.Function?.Name))
            .Select(call => new ToolCallRequest
            {
                Name = call.Function!.Name!,
                ArgumentsJson = NormalizeArguments(call.Function.Arguments)
            })
            .ToList();

        return new ToolChatResult
        {
            Content = message?.Content,
            ToolCalls = toolCalls
        };
    }

    public async Task<float[]> GetEmbeddingAsync(string model, string text, CancellationToken cancellationToken)
    {
        ResetTokenConsumption();
        var requestBody = new
        {
            model,
            input = text,
            encoding_format = "float"
        };

        using var request = CreateJsonRequest(HttpMethod.Post, "v1/embeddings", requestBody);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, model, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<AiHubEmbeddingResponseDto>(stream, JsonOptions, cancellationToken);

        UpdateTokenConsumption(result?.Usage);
        return result?.Data.OrderBy(item => item.Index).FirstOrDefault()?.Embedding ?? [];
    }

    private HttpRequestMessage CreateJsonRequest(HttpMethod method, string relativeUrl, object body)
    {
        var request = new HttpRequestMessage(method, relativeUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static string NormalizeArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(arguments);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            // Preserve the provider payload so the caller can report/handle it.
            return arguments;
        }
    }

    private void ResetTokenConsumption()
    {
        Interlocked.Exchange(ref _promptTokensCount, 0);
        Interlocked.Exchange(ref _responseTokensCount, 0);
    }

    private void UpdateTokenConsumption(AiHubUsageDto? usage)
    {
        if (usage is null)
        {
            return;
        }

        Interlocked.Exchange(ref _promptTokensCount, usage.PromptTokens);
        Interlocked.Exchange(ref _responseTokensCount, usage.CompletionTokens);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string model, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var reason = body;

        try
        {
            var error = JsonSerializer.Deserialize<AiHubErrorEnvelopeDto>(body, JsonOptions);
            if (!string.IsNullOrWhiteSpace(error?.Error?.Message))
            {
                reason = error.Error.Message;
            }
        }
        catch (JsonException)
        {
            // Keep the raw response body.
        }

        throw new InvalidOperationException($"AI Hub request for model '{model}' failed with status " + $"{(int)response.StatusCode} ({response.StatusCode}): {reason}");
    }

    private static List<ChatMessage> NormalizeMessageOrder(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages.Where(message =>
                                    message is not null &&
                                    !string.IsNullOrWhiteSpace(message.Content))
                                .ToList();

        var systemMessages = messageList.Where(message =>
                                        string.Equals(
                                            message.Role,
                                            "system",
                                            StringComparison.OrdinalIgnoreCase))
                                    .ToList();

        var nonSystemMessages = messageList.Where(message =>
                                            !string.Equals(
                                                message.Role,
                                                "system",
                                                StringComparison.OrdinalIgnoreCase))
                                        .ToList();

        if (systemMessages.Count == 0)
        {
            return nonSystemMessages;
        }

        // Some models accept only one system message. Merge all system
        // instructions and place the result at index 0.
        var mergedSystemContent = string.Join(
            Environment.NewLine + Environment.NewLine,
            systemMessages
                .Select(message => message.Content.Trim())
                .Where(content => content.Length > 0));

        var firstSystemMessage = systemMessages[0] with
        {
            Role = "system",
            Content = mergedSystemContent
        };

        nonSystemMessages.Insert(0, firstSystemMessage);
        return nonSystemMessages;
    }
}