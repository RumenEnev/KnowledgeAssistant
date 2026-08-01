using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Contracts.Dto;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/documentation")]
    public class DocumentationController : ControllerBase
    {
        private readonly DocumentationGenerationService _documentationGenerationService;

        public DocumentationController(DocumentationGenerationService documentationGenerationService)
        {
            _documentationGenerationService = documentationGenerationService;
        }

        [HttpPost("save")]
        public async Task<ActionResult<SaveDocumentationResultDto>> Save([FromBody] SaveDocumentationRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Markdown))
            {
                return BadRequest(new { error = "Markdown content is required." });
            }

            if (string.IsNullOrWhiteSpace(request.RelativeFilePath))
            {
                return BadRequest(new { error = "RelativeFilePath is required." });
            }

            try
            {
                var (documentId, savedFilePath) = await _documentationGenerationService.SaveAndIngestAsync(
                    request.RepositoryId, request.RelativeFilePath, request.Title, request.Markdown, cancellationToken);

                return Ok(new SaveDocumentationResultDto { DocumentId = documentId, SavedFilePath = savedFilePath });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (DirectoryNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }
    }
}
