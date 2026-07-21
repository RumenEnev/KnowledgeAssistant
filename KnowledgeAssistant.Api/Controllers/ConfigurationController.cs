using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Dto;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/configuration")]
    public class ConfigurationController : Controller
    {
        private readonly IConfigurationRepository _repository;

        public ConfigurationController(IConfigurationRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("selected-model")]
        public async Task<ActionResult<SelectedModelDto>> GetSelectedModel(CancellationToken cancellationToken)
        {
            var selectedModel = await _repository.GetSelectedModelAsync(cancellationToken);
            return Ok(new SelectedModelDto { SelectedModel = selectedModel });
        }

        [HttpPut("selected-model")]
        public async Task<IActionResult> UpdateSelectedModel([FromBody] UpdateSelectedModelDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.SelectedModel))
            {
                return BadRequest("SelectedModel is required.");
            }

            await _repository.UpsertSelectedModelAsync(request.SelectedModel, cancellationToken);
            return NoContent();
        }

        [HttpGet("chunking-settings")]
        public async Task<ActionResult<ChunkingSettingsDto>> GetChunkingSettings(CancellationToken cancellationToken)
        {
            var (chunkTargetSizeChars, chunkOverlapChars) = await _repository.GetChunkingSettingsAsync(cancellationToken);
            return Ok(new ChunkingSettingsDto
            {
                ChunkTargetSizeChars = chunkTargetSizeChars,
                ChunkOverlapChars = chunkOverlapChars
            });
        }

        [HttpPut("chunking-settings")]
        public async Task<IActionResult> UpdateChunkingSettings([FromBody] ChunkingSettingsDto request, CancellationToken cancellationToken)
        {
            if (request.ChunkTargetSizeChars <= 0)
            {
                return BadRequest("ChunkTargetSizeChars must be greater than zero.");
            }

            if (request.ChunkOverlapChars < 0)
            {
                return BadRequest("ChunkOverlapChars cannot be negative.");
            }

            if (request.ChunkOverlapChars >= request.ChunkTargetSizeChars)
            {
                return BadRequest("ChunkOverlapChars must be smaller than ChunkTargetSizeChars.");
            }

            await _repository.UpsertChunkingSettingsAsync(request.ChunkTargetSizeChars, request.ChunkOverlapChars, cancellationToken);
            return NoContent();
        }
    }
}
