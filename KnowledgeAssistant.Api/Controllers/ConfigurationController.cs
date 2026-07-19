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
    }
}
