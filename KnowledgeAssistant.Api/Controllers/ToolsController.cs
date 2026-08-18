using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Enums;
using KnowledgeAssistant.Contracts.Tools;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/tools")]
public class ToolsController : ControllerBase
{
    private readonly IToolRepository _toolRepository;

    public ToolsController(IToolRepository toolRepository)
    {
        _toolRepository = toolRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ToolDto>>> GetAll([FromQuery] MessageSource? source, CancellationToken cancellationToken)
    {
        var tools = await _toolRepository.GetToolsAsync(source, cancellationToken);
        return Ok(tools.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ToolDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tool = await _toolRepository.GetToolByIdAsync(id, cancellationToken);
        return tool == null ? NotFound() : Ok(ToDto(tool));
    }

    [HttpPost]
    public async Task<ActionResult<ToolDto>> Create(CreateToolDto request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _toolRepository.AddToolAsync(request.Name, request.Description, request.ParametersJsonSchema, request.IsEnabled, cancellationToken);
            var created = await _toolRepository.GetToolByIdAsync(id, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id }, ToDto(created!));
        }
        catch (InvalidOperationException ex)
        {
            // duplicate name
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateToolDto request, CancellationToken cancellationToken)
    {
        var updated = await _toolRepository.UpdateToolAsync(
            id, request.Name, request.Description, request.ParametersJsonSchema, request.IsEnabled, cancellationToken);

        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _toolRepository.DeleteToolAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private ToolDto ToDto(Domain.ToolDefinitionEntity tool) =>
        new(tool.Id, tool.Name, tool.Description, tool.ParametersJsonSchema, tool.IsEnabled, tool.CreatedAt, tool.UpdatedAt);
}
