using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Contracts.Dto;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly DocumentsHandlingService _ingestionService;
        private readonly IDocumentRepository _documentRepository;

        public DocumentsController(DocumentsHandlingService ingestionService, IDocumentRepository documentRepository)
        {
            _ingestionService = ingestionService;
            _documentRepository = documentRepository;
        }

        [HttpPost]
        public async Task<IActionResult> IngestText([FromBody] IngestTextRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Title is required.");

            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("Text is required.");

            if (request.Topics is null || request.Topics.Count == 0)
                return BadRequest("At least one topic is required.");

            try
            {
                var documentId = await _ingestionService.IngestDocumentAsync(
                    request.Title, request.Text, request.Topics, cancellationToken);

                return Ok(new AddDocumentResultDto { DocumentId = documentId });
            }
            catch (InvalidOperationException ex)
            {
                // e.g. a topic name that doesn't exist in rag.topics
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> IngestFile(
            IFormFile file, [FromForm] string title, [FromForm] string topics, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
                return BadRequest("A .txt file is required.");

            if (!file.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only .txt files are supported.");

            if (string.IsNullOrWhiteSpace(title))
                return BadRequest("Title is required.");

            var topicNames = (topics ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (topicNames.Count == 0)
                return BadRequest("At least one topic is required.");

            using var reader = new StreamReader(file.OpenReadStream());
            string text = await reader.ReadToEndAsync(cancellationToken);

            try
            {
                var documentId = await _ingestionService.IngestDocumentAsync(
                    title, text, topicNames, cancellationToken);

                return Ok(new AddDocumentResultDto { DocumentId = documentId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] IngestTextRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Title is required.");

            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("Text is required.");

            if (request.Topics is null || request.Topics.Count == 0)
                return BadRequest("At least one topic is required.");

            try
            {
                await _ingestionService.UpdateDocumentAsync(id, request.Title, request.Text, request.Topics, cancellationToken);
                return Ok(new AddDocumentResultDto { DocumentId = id });
            }
            catch (InvalidOperationException ex)
            {
                // e.g. a topic name that doesn't exist in rag.topics
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var documents = await _documentRepository.GetAllDocumentsAsync(cancellationToken);
            return Ok(documents);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            await _documentRepository.DeleteDocumentAsync(id, cancellationToken);
            return NoContent();
        }

        [HttpGet("topics")]
        public async Task<IActionResult> GetTopics(CancellationToken cancellationToken)
        {
            var topics = await _documentRepository.GetAllTopicsAsync(cancellationToken);
            return Ok(topics);
        }
    }
}