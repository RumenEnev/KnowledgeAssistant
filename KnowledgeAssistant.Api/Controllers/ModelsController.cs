using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Dto;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/models")]
public sealed class ModelsController : ControllerBase
{
    private readonly IModelProviderRegistry _providerRegistry;
    private readonly IModelRepository _modelRepository;

    public ModelsController(IModelProviderRegistry providerRegistry, IModelRepository modelRepository)
    {
        _providerRegistry = providerRegistry;
        _modelRepository = modelRepository;
    }

    /// <summary>
    /// Returns the model providers registered in the API.
    /// </summary>
    [HttpGet("providers")]
    public ActionResult<IReadOnlyCollection<string>> GetProviders()
    {
        return Ok(_providerRegistry.Providers);
    }

    /// <summary>
    /// Returns the selectable models belonging to one provider.
    /// Example: GET api/models?provider=Ollama
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ModelInfoDto>>> Get(
        [FromQuery] string provider,
        CancellationToken cancellationToken)
    {
        if (!TryGetGateway(provider, out var gateway, out var error))
        {
            return error!;
        }

        var models = await gateway.GetModelsAsync(cancellationToken);
        var result = new List<ModelInfoDto>(models.Count);

        foreach (var model in models)
        {
            // If two providers may expose the same model name, evolve the model
            // repository key to (provider, modelName). The current repository API
            // is intentionally preserved here to keep this patch focused.
            var id = await _modelRepository.GetOrCreateModelIdAsync(
                model.Name,
                cancellationToken);

            var flags = await _modelRepository.GetModelFlagsAsync(
                id,
                cancellationToken);

            if (flags.InternalUseOnly)
            {
                continue;
            }

            result.Add(new ModelInfoDto
            {
                Name = model.Name,
                CanCallTools = flags.CanCallTools
            });
        }

        return Ok(result);
    }

    [HttpGet("context-windows")]
    public async Task<ActionResult<IReadOnlyCollection<ModelContextWindowDto>>> GetContextWindows(
        [FromQuery] string provider,
        CancellationToken cancellationToken)
    {
        if (!TryGetGateway(provider, out var gateway, out var error))
        {
            return error!;
        }

        var models = await gateway.GetModelsAsync(cancellationToken);
        var result = new List<ModelContextWindowDto>(models.Count);

        foreach (var model in models)
        {
            var id = await _modelRepository.GetOrCreateModelIdAsync(
                model.Name,
                cancellationToken);

            var flags = await _modelRepository.GetModelFlagsAsync(
                id,
                cancellationToken);

            result.Add(new ModelContextWindowDto
            {
                Id = id,
                Name = model.Name,
                Size = model.Size,
                ContextLength = model.ContextLength,
                Family = model.Family,
                QuantizationLevel = model.QuantizationLevel,
                ParameterSize = model.ParameterSize,
                InternalUseOnly = flags.InternalUseOnly,
                CanCallTools = flags.CanCallTools
            });
        }

        return Ok(result);
    }

    [HttpPut("{id:guid}/context-window")]
    public async Task<IActionResult> UpdateContextWindow(
        Guid id,
        [FromBody] UpdateModelContextWindowDto request,
        CancellationToken cancellationToken)
    {
        await _modelRepository.UpdateModelFlagsAsync(
            id,
            request.InternalUseOnly,
            request.CanCallTools,
            cancellationToken);

        return NoContent();
    }

    private bool TryGetGateway(
        string provider,
        out INamedModelCatalogGateway gateway,
        out ActionResult? error)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            gateway = null!;
            error = BadRequest(new ProblemDetails
            {
                Title = "A model provider is required.",
                Detail = "Supply the provider query parameter, for example '?provider=Ollama'.",
                Status = StatusCodes.Status400BadRequest
            });

            return false;
        }

        if (!_providerRegistry.TryGetCatalogGateway(provider, out gateway!))
        {
            error = NotFound(new ProblemDetails
            {
                Title = "Unknown model provider.",
                Detail = $"Provider '{provider}' is not registered.",
                Status = StatusCodes.Status404NotFound,
                Extensions =
                {
                    ["availableProviders"] = _providerRegistry.Providers
                }
            });

            return false;
        }

        error = null;
        return true;
    }
}
