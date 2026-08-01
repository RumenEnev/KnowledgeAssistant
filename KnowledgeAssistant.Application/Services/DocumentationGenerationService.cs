using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Enums;
using KnowledgeAssistant.Domain;
using KnowledgeAssistant.Domain.Conversation;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace KnowledgeAssistant.Application.Services
{
    /// <summary>
    /// Exposes a "get_source_file_content" tool the LLM can call when the user asks it to document a
    /// source file. When called, locates the file inside a configured repository, reads its content, and
    /// (via <see cref="GenerateDocumentationMarkdownStreamAsync"/>) asks the LLM to produce markdown
    /// documentation for it. Once the user confirms via the UI, <see cref="SaveAndIngestAsync"/> saves the
    /// markdown to disk and ingests it into the RAG index.
    /// </summary>
    public class DocumentationGenerationService
    {
        private const string DocumentationTopicName = "Documentation";
        private const int MaxFileContentChars = 16000;
        private const string GetSourceFileContentToolName = "get_source_file_content";

        private static readonly ToolDefinition GetSourceFileContentTool = new()
        {
            Name = GetSourceFileContentToolName,
            Description =
                "Searches the user's configured code repositories for a source code file by name or relative " +
                "path and returns its full text content. Call this tool whenever the user asks you to write, " +
                "create, or generate documentation for a specific source code file, or asks you to explain/" +
                "document what a particular file does.",
            ParametersJsonSchema = """
                {
                  "type": "object",
                  "properties": {
                    "file_path": {
                      "type": "string",
                      "description": "The file name (e.g. 'UserService.cs') or relative path (e.g. 'src/Services/UserService.cs') of the source file to document."
                    }
                  },
                  "required": ["file_path"]
                }
                """
        };

        private readonly IModelGateway _modelGateway;
        private readonly IConfigurationRepository _configurationRepository;
        private readonly IDocumentRepository _documentRepository;
        private readonly DocumentsHandlingService _documentsHandlingService;
        private readonly ILogger<DocumentationGenerationService> _logger;

        public DocumentationGenerationService(
            IModelGateway modelGateway,
            IConfigurationRepository configurationRepository,
            IDocumentRepository documentRepository,
            DocumentsHandlingService documentsHandlingService,
            ILogger<DocumentationGenerationService> logger)
        {
            _modelGateway = modelGateway;
            _configurationRepository = configurationRepository;
            _documentRepository = documentRepository;
            _documentsHandlingService = documentsHandlingService;
            _logger = logger;
        }

        /// <summary>
        /// Asks the LLM whether the user's message is a request to document a source file by offering it
        /// the "get_source_file_content" tool. Returns the file name/path hint the model supplied if it
        /// chose to call the tool, or null if it didn't (i.e. this isn't a documentation request, or the
        /// selected model doesn't support tool calling).
        /// </summary>
        public async Task<string?> TryDetectRequestAsync(string userMessage, string model, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(userMessage) || string.IsNullOrWhiteSpace(model))
            {
                return null;
            }

            var systemMessage = new ChatMessage
            {
                Role = "system",
                Content =
                    "You are a coding assistant. You have a tool named " +
                    $"'{GetSourceFileContentToolName}' that searches the user's code repositories for a file " +
                    "and returns its content - you do NOT need the user to provide the file path or content " +
                    "yourself. Whenever the user's message asks you to write, create, or generate documentation " +
                    "for a source code file (by name, e.g. 'infrastructure.cs'), or asks you to explain/document " +
                    $"a file, you MUST call the '{GetSourceFileContentToolName}' tool with that file name as the " +
                    "file_path argument. Do not ask the user for clarification or the file content - just call " +
                    "the tool. If the message is not about documenting or explaining a specific file, respond " +
                    "normally without calling any tool."
            };
            var userChatMessage = new ChatMessage { Role = "user", Content = userMessage };

            ToolChatResult result;
            try
            {
                result = await _modelGateway.ChatWithToolsAsync(
                    model, userChatMessage, systemMessage, new[] { GetSourceFileContentTool }, cancellationToken);
            }
            catch (Exception ex)
            {
                // The selected model/gateway may not support tool calling at all (e.g. Ollama rejects the
                // request outright with "model does not support tools") - fall back to the normal chat flow.
                _logger.LogWarning(ex, "Tool-calling request failed for model '{Model}'; falling back to normal chat.", model);
                return null;
            }

            var toolCall = result.ToolCalls.FirstOrDefault(tc => tc.Name == GetSourceFileContentToolName)
                ?? TryParseToolCallFromContent(result.Content);

            if (toolCall is null)
            {
                _logger.LogDebug("Model '{Model}' did not call the '{Tool}' tool for the given message.", model, GetSourceFileContentToolName);
                return null;
            }

            try
            {
                using var argsDoc = JsonDocument.Parse(toolCall.ArgumentsJson);
                if (argsDoc.RootElement.TryGetProperty("file_path", out var fileHintProperty))
                {
                    var hint = fileHintProperty.GetString();
                    return string.IsNullOrWhiteSpace(hint) ? null : hint.Trim();
                }
            }
            catch (JsonException)
            {
                // Malformed tool call arguments - treat as "no tool call".
            }

            return null;
        }

        private static ToolCallRequest? TryParseToolCallFromContent(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(content.Trim());
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    root = root[0];
                }

                if (root.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (!root.TryGetProperty("name", out var nameProperty) ||
                    !string.Equals(nameProperty.GetString(), GetSourceFileContentToolName, StringComparison.Ordinal))
                {
                    return null;
                }

                if (!root.TryGetProperty("arguments", out var argumentsProperty))
                {
                    return null;
                }

                return new ToolCallRequest
                {
                    Name = GetSourceFileContentToolName,
                    ArgumentsJson = argumentsProperty.GetRawText()
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static IEnumerable<string> EnumerateFilesSafely(string root)
        {
            var directories = new Stack<string>();
            directories.Push(root);

            while (directories.Count > 0)
            {
                var current = directories.Pop();
                IEnumerable<string> files = Array.Empty<string>();
                IEnumerable<string> subDirectories = Array.Empty<string>();

                try
                {
                    files = Directory.EnumerateFiles(current);
                    subDirectories = Directory.EnumerateDirectories(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    yield return file;
                }

                foreach (var subDirectory in subDirectories)
                {
                    var name = Path.GetFileName(subDirectory);
                    if (name is ".git" or "bin" or "obj" or "node_modules" or ".vs")
                    {
                        continue;
                    }

                    directories.Push(subDirectory);
                }
            }
        }

        public async Task<string> GenerateDocumentationMarkdownAsync(
            string model,
            string fileName,
            string relativeFilePath,
            string fileContent,
            CancellationToken cancellationToken)
        {
            var sb = new System.Text.StringBuilder();
            await foreach (var token in GenerateDocumentationMarkdownStreamAsync(model, fileName, relativeFilePath, fileContent, cancellationToken))
            {
                sb.Append(token);
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Streams the generated markdown documentation token-by-token, so callers can forward it live to
        /// the user as it's produced (mirroring the normal streamed chat experience).
        /// </summary>
        public async IAsyncEnumerable<string> GenerateDocumentationMarkdownStreamAsync(
            string model,
            string fileName,
            string relativeFilePath,
            string fileContent,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var truncated = fileContent.Length > MaxFileContentChars
                ? fileContent[..MaxFileContentChars] + "\n\n[...truncated...]"
                : fileContent;

            var systemMessage = new ChatMessage
            {
                Role = "system",
                Content = """
                    You are a senior software engineer writing developer documentation.
                    Given the contents of a single source file, produce clear, accurate Markdown documentation for it.
                    Include: a short overview of the file's purpose, its public types/functions/members with brief
                    descriptions, notable behavior or edge cases, and usage notes where relevant.
                    Reply with ONLY the Markdown documentation - no preamble, no code fences wrapping the whole
                    response, and no explanations about what you are doing.
                    """
            };

            var userMessage = new ChatMessage
            {
                Role = "user",
                Content = $"""
                    File path: {relativeFilePath}

                    File contents:
                    ```
                    {truncated}
                    ```
                    """
            };

            var messages = new List<ChatMessage> { systemMessage, userMessage };
            await foreach (var token in _modelGateway.StreamAsync(model, messages, cancellationToken))
            {
                yield return token;
            }
        }


        /// <summary>
        /// Saves the generated markdown next to the source file (under a "_docs" folder that mirrors
        /// the repository's directory structure) and ingests it into the RAG index under the
        /// "Documentation" topic (created automatically if it doesn't exist yet).
        /// </summary>
        public async Task<(int DocumentId, string SavedFilePath)> SaveAndIngestAsync(
            Guid repositoryId,
            string relativeFilePath,
            string title,
            string markdown,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                throw new ArgumentException("Markdown content cannot be empty.", nameof(markdown));
            }

            var repository = await _configurationRepository.GetRepositoryByIdAsync(repositoryId, cancellationToken)
                ?? throw new InvalidOperationException($"Repository with ID {repositoryId} was not found.");

            if (!Directory.Exists(repository.RootPath))
            {
                throw new DirectoryNotFoundException($"Repository root path '{repository.RootPath}' does not exist.");
            }

            var relativeMarkdownPath = Path.ChangeExtension(relativeFilePath, ".md");
            var savedFilePath = Path.Combine(repository.RootPath, "_docs", relativeMarkdownPath);
            var savedDirectory = Path.GetDirectoryName(savedFilePath);
            if (!string.IsNullOrEmpty(savedDirectory))
            {
                Directory.CreateDirectory(savedDirectory);
            }

            await File.WriteAllTextAsync(savedFilePath, markdown, cancellationToken);

            await EnsureDocumentationTopicExistsAsync(cancellationToken);

            var effectiveTitle = string.IsNullOrWhiteSpace(title) ? Path.GetFileName(relativeFilePath) : title;
            var documentId = await _documentsHandlingService.IngestDocumentAsync(
                effectiveTitle,
                markdown,
                DocumentType.Markdown,
                new[] { DocumentationTopicName },
                cancellationToken);

            return (documentId, savedFilePath);
        }

        private async Task EnsureDocumentationTopicExistsAsync(CancellationToken cancellationToken)
        {
            var existingTopicId = await _documentRepository.GetTopicIdByNameAsync(DocumentationTopicName, cancellationToken);
            if (existingTopicId is null)
            {
                await _documentRepository.CreateTopicAsync(DocumentationTopicName, cancellationToken);
            }
        }
    }
}
