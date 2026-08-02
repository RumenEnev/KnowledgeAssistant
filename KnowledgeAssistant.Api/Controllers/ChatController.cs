using KnowledgeAssistant.Api.Streaming;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Domain.Conversation;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : Controller
    {
        private readonly ConversationService _conversationService;
        private readonly IConversationRepository _conversationRepository;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            ConversationService conversationService,
            IConversationRepository conversationRepository,
            ILogger<ChatController> logger)
        {
            _conversationService = conversationService;
            _conversationRepository = conversationRepository;
            _logger = logger;
        }

        [HttpPost]
        public async Task Chat([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
        {
            var writer = new SseWriter(Response);
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                await writer.WriteAsync(SseEvents.Error, new ErrorEventDto
                {
                    Message = "Please select a model before sending a message."
                }, cancellationToken);

                return;
            }

            try
            {
                await writer.WriteAsync(SseEvents.Progress, new ProgressEventDto { Message = "Analyzing your request..." }, cancellationToken);
                var conversationId = await _conversationService.EnsureConversationAsync(request, cancellationToken);
                await writer.WriteAsync(SseEvents.Progress, new ProgressEventDto { Message = "Retrieving relevant context and generating a response..." }, cancellationToken);
                await foreach (var token in _conversationService.GenerateAssistantMessageAsync(conversationId, request.Message, request.Model, cancellationToken))
                {
                    await writer.WriteAsync(SseEvents.Token, new { conversationId, content = token }, cancellationToken);
                }

                var lastAssistantMessage = await _conversationRepository.GetLastAssistantMessageAsync(conversationId, cancellationToken);
                var (promptTokens, responseTokens) = _conversationService.GetTokenConsumption();
                await writer.WriteAsync(SseEvents.MessageCompleted, new ChatResponseChunkDto()
                {
                    ConversationId = conversationId,
                    MessageId = lastAssistantMessage?.Id
                }, cancellationToken);

                await writer.WriteAsync(SseEvents.Done, new MessageDoneDto
                {
                    PromptTokens = promptTokens,
                    ResponseTokens = responseTokens
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client disconnected or cancelled the request; nothing to notify.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while streaming chat response.");

                await writer.WriteAsync(SseEvents.Error, new ErrorEventDto
                {
                    Message = "Something went wrong while processing your message. Please try again."
                }, cancellationToken);
            }
        }

        private async Task HandleDocumentationRequestAsync(SseWriter writer, ChatRequestDto request, string fileHint, CancellationToken cancellationToken)
        {
            await writer.WriteAsync(SseEvents.DocumentationForFile, new ProgressEventDto { Message = fileHint }, cancellationToken);
            var conversationId = await _conversationService.EnsureConversationAsync(request, cancellationToken);
            var userMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = "user",
                Content = request.Message,
                CreatedAt = DateTime.UtcNow
            };
            await _conversationService.CreateMessageAsync(conversationId, userMessage, cancellationToken);

            //var located = await _documentationGenerationService.LocateFileAsync(fileHint, cancellationToken);
            //if (located is null)
            //{
            //    await writer.WriteAsync(SseEvents.Progress, new ProgressEventDto { Message = $"No file matching '{fileHint}' was found in any configured repository." }, cancellationToken);

            //    var notFoundText = $"I couldn't find a file matching '{fileHint}' in any configured repository. " +
            //                        "Make sure the repository containing it is registered and the file name is correct.";

            //    await writer.WriteAsync(SseEvents.Token, new { conversationId, content = notFoundText }, cancellationToken);
            //    await _conversationService.CreateMessageAsync(conversationId, new ChatMessage
            //    {
            //        Id = Guid.NewGuid(),
            //        ConversationId = conversationId,
            //        Role = "assistant",
            //        Content = notFoundText,
            //        CreatedAt = DateTime.UtcNow
            //    }, cancellationToken);

            //    await writer.WriteAsync(SseEvents.Done, new MessageDoneDto { PromptTokens = 0, ResponseTokens = 0 }, cancellationToken);
            //    return;
            //}

            //await writer.WriteAsync(SseEvents.Progress, new ProgressEventDto { Message = $"Found '{located.RelativeFilePath}' in repository '{located.Repository.Name}'. Reading file content..." }, cancellationToken);

            //var fileContent = await System.IO.File.ReadAllTextAsync(located.FullPath, cancellationToken);

            //await writer.WriteAsync(SseEvents.Progress, new ProgressEventDto { Message = "Generating documentation..." }, cancellationToken);

            //var markdown = await _documentationGenerationService.GenerateDocumentationMarkdownAsync(
            //    request.Model!, located.FileName, located.RelativeFilePath, fileContent, cancellationToken);

            //var chatNote = $"I've generated documentation for '{located.RelativeFilePath}'. " +
            //                "It's opened in a separate Documentation window for your review - " +
            //                "confirm there to save it to disk and ingest it into the RAG index.";

            //await writer.WriteAsync(SseEvents.Token, new { conversationId, content = chatNote }, cancellationToken);
            //await _conversationService.CreateMessageAsync(conversationId, new ChatMessage
            //{
            //    Id = Guid.NewGuid(),
            //    ConversationId = conversationId,
            //    Role = "assistant",
            //    Content = chatNote,
            //    CreatedAt = DateTime.UtcNow
            //}, cancellationToken);

            //await writer.WriteAsync(SseEvents.Progress, new ProgressEventDto { Message = "Documentation generated. Review it in the Documentation window and confirm to save & ingest." }, cancellationToken);

            //await writer.WriteAsync(SseEvents.Documentation, new DocumentationEventDto
            //{
            //    RepositoryId = located.Repository.Id,
            //    RepositoryName = located.Repository.Name,
            //    FileName = located.FileName,
            //    RelativeFilePath = located.RelativeFilePath,
            //    Title = $"Documentation: {located.FileName}",
            //    Markdown = markdown
            //}, cancellationToken);

            //await writer.WriteAsync(SseEvents.Done, new MessageDoneDto { PromptTokens = 0, ResponseTokens = 0 }, cancellationToken);
        }

        [HttpPost("title")]
        public async Task<IActionResult> GenerateTitle([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Model))
            {
                return BadRequest("Model is required.");
            }

            var title = await _conversationService.GenerateTitleAsync(request.Message, request.Model, cancellationToken);
            return Ok(new ConversationDto { Title = title });
        }
    }
}

