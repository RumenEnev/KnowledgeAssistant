namespace KnowledgeAssistant.Contracts.Tools;

public record ToolDto(Guid Id, string Name, string Description, string ParametersJsonSchema, bool IsEnabled, DateTime CreatedAt, DateTime UpdatedAt);

public record CreateToolDto(string Name, string Description, string ParametersJsonSchema, bool IsEnabled);

public record UpdateToolDto(string? Name, string? Description, string? ParametersJsonSchema, bool? IsEnabled);
