using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Contracts.Dto;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/models")]
    public class ModelsController : ControllerBase
    {
        private readonly ModelCatalogService _service;
        private readonly IModelRepository _modelRepository;

        public ModelsController(ModelCatalogService service, IModelRepository modelRepository)
        {
            _service = service;
            _modelRepository = modelRepository;
        }

        [HttpGet]
        public async Task<IEnumerable<ModelInfoDto>> Get(CancellationToken cancellationToken)
        {
            var models = await _service.GetModelsAsync(cancellationToken);
            return models.Select(x => new ModelInfoDto
            {
                Name = x.Name
            });
        }

        [HttpGet("context-windows")]
        public async Task<IEnumerable<ModelContextWindowDto>> GetContextWindows(CancellationToken cancellationToken)
        {
            var models = await _service.GetModelsAsync(cancellationToken);
            var result = new List<ModelContextWindowDto>();
            foreach (var model in models)
            {
                var id = await _modelRepository.GetOrCreateModelIdAsync(model.Name, cancellationToken);
                result.Add(new ModelContextWindowDto
                {
                    Id = id,
                    Name = model.Name,
                    Size = model.Size,
                    ContextLength = model.ContextLength,
                    Family = model.Family,
                    QuantizationLevel = model.QuantizationLevel,
                    ParameterSize = model.ParameterSize
                });
            }

            return result;
        }

        [HttpPut("{id}/context-window")]
        public async Task<IActionResult> UpdateContextWindow(Guid id, [FromBody] UpdateModelContextWindowDto request, CancellationToken cancellationToken)
        {
            if (request.ContextWindowTokens.HasValue && request.ContextWindowTokens.Value <= 0)
            {
                return BadRequest("Context window tokens must be greater than zero.");
            }

            await _modelRepository.UpdateContextWindowTokensAsync(id, request.ContextWindowTokens, cancellationToken);
            return NoContent();
        }
    }
}