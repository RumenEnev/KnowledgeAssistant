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

        public ModelsController(ModelCatalogService service)
        {
            _service = service;
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
    }
}