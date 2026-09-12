using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Dto;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/configuration")]
public sealed class ConfigurationController : ControllerBase
{
    private readonly IConfigurationRepository _repository;
    private readonly IModelRepository _modelRepository;

    public ConfigurationController(IConfigurationRepository repository, IModelRepository modelRepository)
    {
        _repository = repository;
        _modelRepository = modelRepository;
    }

    [HttpGet("chunking-settings")]
    public async Task<ActionResult<ChunkingSettingsDto>> GetChunkingSettings(CancellationToken cancellationToken)
    {
        var chunkingSettings = await _repository.GetChunkingSettingsAsync(cancellationToken);
        return Ok(new ChunkingSettingsDto
        {
            EmbeddingModelName = chunkingSettings.EmbeddingModelName,
            ChunkTargetSizeChars = chunkingSettings.ChunkTargetSizeChars,
            ChunkOverlapChars = chunkingSettings.ChunkOverlapChars
        });
    }

    [HttpPut("chunking-settings")]
    public async Task<IActionResult> UpdateChunkingSettings([FromBody] ChunkingSettingsDto request, CancellationToken cancellationToken)
    {
        if (request.ChunkTargetSizeChars <= 0)
        {
            return BadRequest("ChunkTargetSizeChars must be greater than zero.");
        }

        if (request.ChunkOverlapChars >= request.ChunkTargetSizeChars)
        {
            return BadRequest("ChunkOverlapChars must be smaller than ChunkTargetSizeChars.");
        }

        var modelId = await _modelRepository.GetOrCreateModelIdAsync(request.EmbeddingModelName, cancellationToken);
        await _repository.UpsertChunkingSettingsAsync(modelId, request.ChunkTargetSizeChars, request.ChunkOverlapChars, cancellationToken);
        return NoContent();
    }
}