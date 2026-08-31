using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Domain.Documents;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/documents/{documentId:int}/retrieval-config")]
public sealed class DocumentRetrievalConfigController : ControllerBase
{
    private readonly DocumentsHandlingService _documentsHandlingService;

    public DocumentRetrievalConfigController(DocumentsHandlingService documentsHandlingService)
    {
        _documentsHandlingService = documentsHandlingService;
    }

    [HttpGet]
    public async Task<ActionResult<DocumentRetrievalConfig>> Get(int documentId, CancellationToken cancellationToken)
    {
        var config = await _documentsHandlingService.GetDocumentRetrievalConfigAsync(documentId, cancellationToken);
        return Ok(config);
    }

    [HttpPut]
    public async Task<IActionResult> Put(int documentId, [FromBody] DocumentRetrievalConfig config, CancellationToken cancellationToken)
    {
        if (documentId != config.DocumentId)
        {
            return BadRequest("Route document ID and body document ID must match.");
        }

        await _documentsHandlingService.SaveDocumentRetrievalConfigAsync(config, cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int documentId, CancellationToken cancellationToken)
    {
        await _documentsHandlingService.ResetDocumentRetrievalConfigAsync(documentId, cancellationToken);
        return NoContent();
    }
}