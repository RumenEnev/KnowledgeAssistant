namespace KnowledgeAssistant.Contracts.Repositories;

public record RepositoryDto(Guid Id, string Name, string RootPath, string? Description, DateTime CreatedAt, DateTime UpdatedAt);

public record CreateRepositoryDto(string Name, string RootPath, string? Description);

public record UpdateRepositoryDto(string? Name, string? RootPath, string? Description);