using KnowledgeAssistant.Domain;

namespace KnowledgeAssistant.Contracts.Tools;

public record ToolDto(Guid Id, string Name, string Description, string ParametersJsonSchema, bool IsEnabled, DateTime CreatedAt, DateTime UpdatedAt, ToolScope Scope, string? Path);

public record CreateToolDto(string Name, string Description, string ParametersJsonSchema, bool IsEnabled, ToolScope Scope = ToolScope.Desktop, string? Path = null);

public record UpdateToolDto(string? Name, string? Description, string? ParametersJsonSchema, bool? IsEnabled, ToolScope? Scope = null, string? Path = null);
