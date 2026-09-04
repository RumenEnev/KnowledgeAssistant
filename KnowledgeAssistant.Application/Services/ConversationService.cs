using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Contracts.Dto.Conversation;
using KnowledgeAssistant.Contracts.Enums;
using KnowledgeAssistant.Domain;
using KnowledgeAssistant.Domain.Conversation;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;

namespace KnowledgeAssistant.Application.Services
{
    public class ConversationService
    {
        private const int TopicClassificationUserMessageThreshold = 2;

        private readonly IModelGatewayResolver _modelGatewayResolver;
        private readonly IConversationRepository _repository;
        private readonly IModelRepository _modelRepository;
        private readonly IDocumentRepository _documentRepository;
        private readonly DocumentsHandlingService _documentsHandlingService;
        private readonly IToolRepository _toolRepository;
        private readonly IToolExecutor _toolExecutor;
        private readonly IToolExecutionService _toolExecutionService;
        private readonly ILogger<ConversationService> _logger;
        private int _promptTokensCount;
        private int _responseTokensCount;

        public ConversationService(IModelGatewayResolver modelGatewayResolver,
                                    IConversationRepository repository,
                                    IModelRepository modelRepository,
                                    IDocumentRepository documentRepository,
                                    DocumentsHandlingService documentsHandlingService,
                                    IToolRepository toolRepository,
                                    IToolExecutor toolExecutor,
                                    IToolExecutionService toolExecutionService,
                                    ILogger<ConversationService> logger)
        {
            _modelGatewayResolver = modelGatewayResolver;
            _repository = repository;
            _modelRepository = modelRepository;
            _documentRepository = documentRepository;
            _documentsHandlingService = documentsHandlingService;
            _toolRepository = toolRepository;
            _toolExecutor = toolExecutor;
            _toolExecutionService = toolExecutionService;
            _logger = logger;
        }

        public async Task<string> GenerateTitleAsync(Guid conversationId,string userMessage, string model, CancellationToken cancellationToken)
        {
            const int maxTitleWords = 6;
            const int maxTitleLength = 60;
            string? generated = null;
            var modelGateway = await GetModelGatewayAsync(conversationId, cancellationToken);
            try
            {
                generated = await modelGateway.GenerateAsync(
                    model: model,
                    systemMessage: new ChatMessage
                    {
                        Role = "system",
                        Content = "You generate short conversation titles. Reply with ONLY the title text: " +
                                   $"at most {maxTitleWords} words, no quotes, no punctuation at the end, no explanations. " +
                                   "Do not answer the user's message."
                    },
                    userMessage: new ChatMessage
                    {
                        Role = "user",
                        Content = userMessage
                    },
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Fall through to the fallback title below.
            }

            var title = SanitizeTitle(generated, maxTitleWords, maxTitleLength);
            return !string.IsNullOrWhiteSpace(title) ? title : SanitizeTitle(userMessage, maxTitleWords, maxTitleLength);
        }

        private static string SanitizeTitle(string? text, int maxWords, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? string.Empty;

            var trimmed = firstLine.Trim().Trim('"', '\'', '.', ' ');

            var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var title = words.Length > maxWords
                ? string.Join(' ', words.Take(maxWords))
                : string.Join(' ', words);

            return title.Length > maxLength ? title[..maxLength].TrimEnd() : title;
        }

        public async Task<Guid> EnsureConversationAsync(ChatRequestDto request, CancellationToken cancellationToken)
        {
            if (request.ConversationId.HasValue)
            {
                var existing = await _repository.GetAsync(request.ConversationId.Value, cancellationToken);
                if (existing is not null)
                {
                    return existing.Id;
                }
            }

            var conversation = new Conversation()
            {
                Id = Guid.NewGuid(),
                Title = await GenerateTitleAsync((Guid)request.ConversationId, request.Message, request.Model ?? "llama3", cancellationToken),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SelectedModelId = await _modelRepository.GetOrCreateModelIdAsync(request.Model ?? "llama3", cancellationToken),
                Provider = ModelProviderNames.Unknown
            };

            await _repository.CreateAsync(conversation, cancellationToken);
            return conversation.Id;
        }

        public async Task CreateMessageAsync(Guid conversationId, ChatMessage message, CancellationToken cancellationToken)
        {
            await _repository.CreateMessageAsync(conversationId, message, cancellationToken);
        }

        public async IAsyncEnumerable<string> GenerateAssistantMessageAsync(Guid conversationId, string message, string model, MessageSource source, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var conversation = await _repository.GetAsync(conversationId, cancellationToken);
            var modelGateway = await GetModelGatewayAsync(conversationId, cancellationToken);
            if (conversation is null)
            {
                throw new InvalidOperationException($"Conversation with ID {conversationId} not found.");
            }

            _logger.LogInformation("Generating assistant message for conversation {ConversationId} from {Source} client.", conversationId, source);
            var relevantContext = await _documentsHandlingService.GetRelevantContextAsync(model, conversation.Topic, message, cancellationToken);
            var userMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                Content = message,
                ConversationId = conversationId,
                Role = "user",
                CreatedAt = DateTime.UtcNow
            };

            var messages = conversation.Messages?.ToList() ?? new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(relevantContext))
            {
                var contextMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    Content = $"""
                                Use the following retrieved context to help answer the user's question.
                                If the context is not relevant or insufficient, rely on your own knowledge but say so.

                                --- Retrieved Context ---
                                {relevantContext}
                                --- End Context ---
                                """,
                    ConversationId = conversationId,
                    Role = "system",
                    CreatedAt = DateTime.UtcNow
                };

                messages.Add(contextMessage);
            }

            var toolResultsMessage = await TryExecuteToolsAsync(conversationId, message, model, source, cancellationToken);
            if (toolResultsMessage is not null)
            {
                messages.Add(toolResultsMessage);
            }

            messages.Add(userMessage);

            var selectedModel = model;
            var buffer = new StringBuilder();
            var modelId = await _modelRepository.GetOrCreateModelIdAsync(selectedModel, cancellationToken);
            await _repository.UpdateSelectedModelAsync(conversationId, modelId, cancellationToken);
            await foreach (var token in modelGateway.StreamAsync(selectedModel, messages, cancellationToken))
            {
                buffer.Append(token);
                yield return token;
            }

            (_promptTokensCount, _responseTokensCount) = modelGateway.GetTokenConsumption();
            userMessage.TokensCount = _promptTokensCount;
            var assistantMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = "assistant",
                Content = buffer.ToString(),
                CreatedAt = DateTime.UtcNow,
                TokensCount = _responseTokensCount,
            };

            await _repository.CreateMessageAsync(conversationId, userMessage, cancellationToken);
            await _repository.CreateMessageAsync(conversationId, assistantMessage, cancellationToken);
            var userMessageCount = messages.Count(m => m.Role == "user");
            if (conversation.TopicId is null && userMessageCount == TopicClassificationUserMessageThreshold)
            {
                await ClassifyTopicAsync(conversationId, messages, selectedModel, cancellationToken);
            }
        }

        private async Task<ChatMessage?> TryExecuteToolsAsync(Guid conversationId, string message, string model, MessageSource source, CancellationToken cancellationToken)
        {
            var enabledTools = await _toolRepository.GetEnabledToolsAsync(source, cancellationToken);
            var modelGateway = await GetModelGatewayAsync(conversationId, cancellationToken);
            if (enabledTools.Count == 0)
            {
                return null;
            }

            var toolDefinitions = enabledTools.Select(t => new ToolDefinition
            {
                Name = t.Name,
                Description = t.Description,
                ParametersJsonSchema = t.ParametersJsonSchema
            }).ToList();

            ToolChatResult toolCheckResult;
            try
            {
                toolCheckResult = await modelGateway.ChatWithToolsAsync(
                    model: model,
                    userMessage: new ChatMessage { Role = "user", Content = message },
                    systemMessage: new ChatMessage
                    {
                        Role = "system",
                        Content = "You can call tools when the user's request clearly needs one. " +
                                   "Only call a tool if it is relevant to the user's message; otherwise reply normally without calling any tool."
                    },
                    tools: toolDefinitions,
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Tool-call detection is a best-effort enhancement; fall back to the normal chat flow on failure.
                _logger.LogError(ex, "Tool-call detection failed for model {Model}.", model);
                return null;
            }

            if (!toolCheckResult.HasToolCalls)
            {
                return null;
            }

            var resultSections = new List<string>();
            foreach (var call in toolCheckResult.ToolCalls)
            {
                var tool = enabledTools.FirstOrDefault(t => string.Equals(t.Name, call.Name, StringComparison.OrdinalIgnoreCase));
                if (tool is null)
                {
                    continue;
                }

                try
                {
                    var toolResult = tool.Scope == ToolScope.All
                        ? await _toolExecutionService.ExecuteAsync(tool, call.ArgumentsJson, cancellationToken)
                        : await _toolExecutor.ExecuteAsync(tool, call.ArgumentsJson, cancellationToken);
                    resultSections.Add($"Tool `{tool.Name}` returned:\n{toolResult}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool '{ToolName}' execution failed.", tool.Name);
                    resultSections.Add($"Tool `{tool.Name}` failed: {ex.Message}");
                }
            }

            if (resultSections.Count == 0)
            {
                return null;
            }

            return new ChatMessage
            {
                Id = Guid.NewGuid(),
                Content = $"""
                            The following tool results are available. For each one:
                            - If the result contains data meant to be shown to the user (e.g. a list, a lookup value),
                              present that data clearly.
                            - If the result is only a status/confirmation (e.g. success/error with no content field),
                              simply confirm the outcome in one or two sentences. Do NOT invent, guess, or reproduce
                              any content, code, or details that are not literally present in the result JSON below.
                            - If a tool failed, tell the user what went wrong based on the reason/message given.

                            --- Tool Results ---
                            {string.Join("\n\n", resultSections)}
                            --- End Tool Results ---
                            """,
                ConversationId = conversationId,
                Role = "system",
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task ClassifyTopicAsync(Guid conversationId, IEnumerable<ChatMessage> messages, string model, CancellationToken cancellationToken)
        {
            var modelGateway = await GetModelGatewayAsync(conversationId, cancellationToken);
            try
            {
                var topics = (await _documentRepository.GetAllTopicsAsync(cancellationToken)).ToList();
                if (topics.Count == 0)
                {
                    return;
                }

                var topicNames = string.Join(", ", topics.Select(t => t.Name));
                var transcript = string.Join(
                    "\n",
                    messages.Select(m => $"{m.Role}: {m.Content}"));

                string? generated = null;
                try
                {
                    generated = await modelGateway.GenerateAsync(
                        model: model,
                        systemMessage: new ChatMessage
                        {
                            Role = "system",
                            Content = "You classify a conversation into exactly one topic from a fixed list, based on what it is about. " +
                                       $"Available topics: {topicNames}. " +
                                       "Reply with ONLY the exact topic name from the list that best matches the conversation. " +
                                       "If none of the topics reasonably match, reply with exactly: NONE. " +
                                       "Do not explain, do not add punctuation, do not invent new topic names."
                        },
                        userMessage: new ChatMessage
                        {
                            Role = "user",
                            Content = transcript
                        },
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    return;
                }

                var candidate = generated?.Trim().Trim('"', '\'', '.', ' ');
                if (string.IsNullOrWhiteSpace(candidate) || string.Equals(candidate, "NONE", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var matchedTopic = topics.FirstOrDefault(t => string.Equals(t.Name, candidate, StringComparison.OrdinalIgnoreCase));
                if (matchedTopic is null)
                {
                    return;
                }

                await _repository.UpdateTopicAsync(conversationId, matchedTopic.Id, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Topic classification is a best-effort enhancement; ignore failures so chat is not disrupted.
            }
        }

        public (int, int) GetTokenConsumption()
        {
            return (_promptTokensCount, _responseTokensCount);
        }

        private async Task<IModelGateway> GetModelGatewayAsync(Guid conversationId, CancellationToken cancellationToken)
        {
            var conversation = await _repository.GetAsync(conversationId, cancellationToken);
            if (conversation is null)
            {
                throw new InvalidOperationException($"Conversation '{conversationId}' was not found.");
            }

            var provider = conversation.Provider;
            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new InvalidOperationException($"Conversation '{conversationId}' does not have " + "a selected model provider.");
            }

            return _modelGatewayResolver.GetRequiredGateway(provider);
        }
    }
}