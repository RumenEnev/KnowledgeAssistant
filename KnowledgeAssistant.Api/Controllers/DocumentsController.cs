using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Contracts.Dto;
using KnowledgeAssistant.Contracts.Enums;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers;

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
        {
            return BadRequest("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Text is required.");
        }

        if (request.Topics is null || request.Topics.Count == 0)
        {
            return BadRequest("At least one topic is required.");
        }

        try
        {
            var (documentId, chunksCount) = await _ingestionService.IngestDocumentAsync(request.Title, request.Text, request.EmbeddingModel, request.DocumentType, request.Topics, cancellationToken);
            return Ok(new AddDocumentResultDto { DocumentId = documentId, ChunksCount = chunksCount });
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
        {
            return BadRequest("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Text is required.");
        }

        if (request.Topics is null || request.Topics.Count == 0)
        {
            return BadRequest("At least one topic is required.");
        }

        try
        {
            await _ingestionService.UpdateDocumentAsync(id, request.Title, request.Text, request.EmbeddingModel, request.DocumentType, request.Topics, cancellationToken);
            return Ok(new AddDocumentResultDto { DocumentId = id, ChunksCount = 0 });
        }
        catch (InvalidOperationException ex)
        {
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