using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeAssistant.Api.Controllers;

[ApiController]
[Route("api/repositories")]
public class RepositoriesController : ControllerBase
{
    private readonly IConfigurationRepository _configurationRepository;

    public RepositoriesController(IConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RepositoryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var repos = await _configurationRepository.GetRepositoriesAsync(cancellationToken);
        return Ok(repos.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RepositoryDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var repo = await _configurationRepository.GetRepositoryByIdAsync(id, cancellationToken);
        return repo == null ? NotFound() : Ok(ToDto(repo));
    }

    [HttpPost]
    public async Task<ActionResult<RepositoryDto>> Create(CreateRepositoryDto request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _configurationRepository.AddRepositoryAsync(
                request.Name, request.RootPath, request.Description, cancellationToken);

            var created = await _configurationRepository.GetRepositoryByIdAsync(id, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id }, ToDto(created!));
        }
        catch (InvalidOperationException ex)
        {
            // duplicate name
            return Conflict(new { error = ex.Message });
        }
        catch (DirectoryNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateRepositoryDto request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _configurationRepository.UpdateRepositoryAsync(
                id, request.Name, request.RootPath, request.Description, cancellationToken);

            return updated ? NoContent() : NotFound();
        }
        catch (DirectoryNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _configurationRepository.DeleteRepositoryAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private RepositoryDto ToDto(Domain.SourceRepository repo) =>
        new(repo.Id, repo.Name, repo.RootPath, repo.Description, repo.CreatedAt, repo.UpdatedAt);
}