using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Dto;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/topics")]
    public class TopicsController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;

        public TopicsController(IDocumentRepository documentRepository)
        {
            _documentRepository = documentRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var topics = await _documentRepository.GetAllTopicsAsync(cancellationToken);
            return Ok(topics);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TopicRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Name is required.");
            }

            try
            {
                var topic = await _documentRepository.CreateTopicAsync(request.Name.Trim(), cancellationToken);
                return Ok(topic);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] TopicRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Name is required.");
            }

            try
            {
                var updated = await _documentRepository.UpdateTopicAsync(id, request.Name.Trim(), cancellationToken);
                if (!updated)
                {
                    return NotFound();
                }

                return Ok(new KnowledgeAssistant.Domain.Documents.Topic { Id = id, Name = request.Name.Trim() });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            try
            {
                await _documentRepository.DeleteTopicAsync(id, cancellationToken);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }
    }
}
