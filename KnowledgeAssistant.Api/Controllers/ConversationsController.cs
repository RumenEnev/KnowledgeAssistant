using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Definitions;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Domain.Conversation;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController : ControllerBase
{
    private readonly IConversationRepository _repository;
    private readonly IModelRepository _modelRepository;
    private readonly IModelProviderRegistry _providerRegistry;

    public ConversationsController(
        IConversationRepository repository,
        IModelRepository modelRepository,
        IModelProviderRegistry providerRegistry)
    {
        _repository = repository;
        _modelRepository = modelRepository;
        _providerRegistry = providerRegistry;
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations(
        CancellationToken cancellationToken)
    {
        var conversations = await _repository.GetAllAsync(cancellationToken);
        var conversationDtos = conversations.Select(conversation => new ConversationDto
        {
            Id = conversation.Id,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt,
            SelectedProvider = conversation.Provider,
            TopicId = conversation.TopicId,
            Topic = conversation.Topic
        });

        return Ok(conversationDtos.OrderByDescending(conversation => conversation.CreatedAt));
    }

    [HttpGet("{conversationId:guid}")]
    public async Task<IActionResult> GetConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetAsync(
            conversationId,
            cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        var selectedModel = await GetSelectedModelNameAsync(
            conversation,
            cancellationToken);

        return Ok(ToDto(conversation, selectedModel));
    }

    [HttpPost]
    public async Task<IActionResult> CreateConversation(
        CancellationToken cancellationToken)
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = "New Conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Provider = ModelProviderNames.Unknown
        };

        await _repository.CreateAsync(conversation, cancellationToken);

        return Ok(ToDto(conversation, selectedModel: null));
    }

    /// <summary>
    /// Atomically stores the provider/model pair on one conversation.
    /// </summary>
    [HttpPut("{conversationId:guid}/model-selection")]
    public async Task<IActionResult> UpdateModelSelection(
        Guid conversationId,
        [FromBody] UpdateConversationModelSelectionDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedProvider))
        {
            ModelState.AddModelError(
                nameof(request.SelectedProvider),
                "SelectedProvider is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SelectedModel))
        {
            ModelState.AddModelError(
                nameof(request.SelectedModel),
                "SelectedModel is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!_providerRegistry.TryGetCatalogGateway(
                request.SelectedProvider,
                out var catalogGateway))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unknown model provider.",
                Detail = $"Provider '{request.SelectedProvider}' is not registered.",
                Status = StatusCodes.Status400BadRequest,
                Extensions =
                {
                    ["availableProviders"] = _providerRegistry.Providers
                }
            });
        }

        var conversation = await _repository.GetAsync(
            conversationId,
            cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        // Validate the pair, so a model from Ollama cannot accidentally be saved
        // together with AdessoAiHub (or vice versa).
        var availableModels = await catalogGateway.GetModelsAsync(cancellationToken);
        var selectedModel = availableModels.FirstOrDefault(model =>
            string.Equals(
                model.Name,
                request.SelectedModel,
                StringComparison.Ordinal));

        if (selectedModel is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Model is not available from the selected provider.",
                Detail = $"Model '{request.SelectedModel}' was not returned by provider '{request.SelectedProvider}'.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var modelId = await _modelRepository.GetOrCreateModelIdAsync(
            selectedModel.Name,
            cancellationToken);

        conversation.Provider = catalogGateway.Provider;
        conversation.SelectedModelId = modelId;
        conversation.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(conversation, cancellationToken);

        return NoContent();
    }

    [HttpPatch("{conversationId:guid}/title")]
    public async Task<IActionResult> UpdateConversationTitle(
        Guid conversationId,
        [FromQuery] string newTitle,
        CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetAsync(
            conversationId,
            cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        conversation.Title = newTitle;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(conversation, cancellationToken);

        var selectedModel = await GetSelectedModelNameAsync(
            conversation,
            cancellationToken);

        return Ok(ToDto(conversation, selectedModel));
    }

    [HttpPatch("{conversationId:guid}/topic")]
    public async Task<IActionResult> UpdateConversationTopic(
        Guid conversationId,
        [FromQuery] int? topicId,
        CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetAsync(
            conversationId,
            cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        await _repository.UpdateTopicAsync(
            conversationId,
            topicId,
            cancellationToken);

        var updatedConversation = await _repository.GetAsync(
            conversationId,
            cancellationToken);

        var selectedModel = await GetSelectedModelNameAsync(
            updatedConversation!,
            cancellationToken);

        return Ok(ToDto(updatedConversation!, selectedModel));
    }

    [HttpDelete("{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await _repository.GetAsync(
            conversationId,
            cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        var deletedConversationId = await _repository.DeleteConversationAsync(
            conversationId,
            cancellationToken);

        if (deletedConversationId == Guid.Empty)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "An error occurred while deleting the conversation.");
        }

        return Ok(deletedConversationId);
    }

    private async Task<string?> GetSelectedModelNameAsync(
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        if (!conversation.SelectedModelId.HasValue ||
            conversation.SelectedModelId.Value == Guid.Empty)
        {
            return null;
        }

        return await _modelRepository.GetModelNameAsync(
            conversation.SelectedModelId.Value,
            cancellationToken);
    }

    private static ConversationDto ToDto(Conversation conversation, string? selectedModel)
    {
        return new ConversationDto
        {
            Id = conversation.Id,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt,
            SelectedProvider = conversation.Provider,
            SelectedModel = selectedModel,
            TopicId = conversation.TopicId,
            Topic = conversation.Topic,
            Messages = conversation.Messages?.Select(message => new MessageDto
            {
                Id = message.Id,
                Role = message.Role,
                Content = message.Content,
                CreatedAt = message.CreatedAt
            })
        };
    }
}