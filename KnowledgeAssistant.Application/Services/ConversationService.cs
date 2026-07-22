using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Domain.Conversation;
using System.Runtime.CompilerServices;
using System.Text;

namespace KnowledgeAssistant.Application.Services
{
    public class ConversationService
    {
        private const int TopicClassificationUserMessageThreshold = 2;

        private readonly IModelGateway _modelGateway;
        private readonly IConversationRepository _repository;
        private readonly IModelRepository _modelRepository;
        private readonly IDocumentRepository _documentRepository;

        private int _promptTokensCount;
        private int _responseTokensCount;

        public ConversationService(IModelGateway modelGateway, IConversationRepository repository, IModelRepository modelRepository, IDocumentRepository documentRepository)
        {
            _modelGateway = modelGateway;
            _repository = repository;
            _modelRepository = modelRepository;
            _documentRepository = documentRepository;
        }

        public async Task<string> GenerateTitleAsync(string userMessage, string model, CancellationToken cancellationToken)
        {
            const int maxTitleWords = 6;
            const int maxTitleLength = 60;

            string? generated = null;
            try
            {
                generated = await _modelGateway.GenerateAsync(
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
                Title = await GenerateTitleAsync(request.Message, request.Model ?? "llama3", cancellationToken),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SelectedModelId = await _modelRepository.GetOrCreateModelIdAsync(request.Model ?? "llama3", cancellationToken)
            };

            await _repository.CreateAsync(conversation, cancellationToken);
            return conversation.Id;
        }

        public async Task CreateMessageAsync(Guid conversationId, ChatMessage message, CancellationToken cancellationToken)
        {
            await _repository.CreateMessageAsync(conversationId, message, cancellationToken);
        }

        public async IAsyncEnumerable<string> SendMessageAsync(Guid conversationId, string message, string? model, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var userMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                Content = message,
                ConversationId = conversationId,
                Role = "user",
                CreatedAt = DateTime.UtcNow
            };

            var conversation = await _repository.GetAsync(conversationId, cancellationToken);
            var messages = conversation!.Messages?.ToList() ?? new List<ChatMessage>();
            messages.Add(userMessage);

            var selectedModel = model ?? "llama3";
            var buffer = new StringBuilder();

            var modelId = await _modelRepository.GetOrCreateModelIdAsync(selectedModel, cancellationToken);
            await _repository.UpdateSelectedModelAsync(conversationId, modelId, cancellationToken);

            // 3. Stream tokens from model
            await foreach (var token in _modelGateway.StreamAsync(selectedModel, messages, cancellationToken))
            {
                buffer.Append(token);
                yield return token;
            }

            // 4. Persist assistant message (optional but recommended)
            (_promptTokensCount, _responseTokensCount) = _modelGateway.GetTokenConsumption();
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
            if (conversation!.TopicId is null && userMessageCount == TopicClassificationUserMessageThreshold)
            {
                await ClassifyTopicAsync(conversationId, messages, selectedModel, cancellationToken);
            }
        }

        private async Task ClassifyTopicAsync(Guid conversationId, IEnumerable<ChatMessage> messages, string model, CancellationToken cancellationToken)
        {
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
                    generated = await _modelGateway.GenerateAsync(
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
    }
}