using KnowledgeAssistant.Wpf.Messages.Conversations;
using KnowledgeAssistant.Wpf.Messages.Files;
using KnowledgeAssistant.Wpf.Models.Files.CSharp;
using MessageServices;
using System.Collections.Concurrent;
using System.IO;

namespace KnowledgeAssistant.Wpf.Services
{
    public class FilesProcessingService : IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;

        public FilesProcessingService(MessageService messageService)
        {
            _messageService = messageService;

            _messageService.Subscribe<CreateFileDocumentationRequest>(this, CreateFileDocumentationRequestReceived);
        }

        private void CreateFileDocumentationRequestReceived(MessageBase message)
        {
            if (message is CreateFileDocumentationRequest request)
            {
                var results = new ConcurrentBag<string>();
                if (request.Repositories.Any())
                {
                    Parallel.ForEach(request.Repositories, folder =>
                    {
                        if (!Directory.Exists(folder))
                        {
                            return;
                        }

                        try
                        {
                            var allFiles = Directory.EnumerateFiles(folder, "*", System.IO.SearchOption.AllDirectories);
                            foreach (var file in allFiles)
                            {
                                if (string.Equals(Path.GetFileName(file), request.FileName, StringComparison.OrdinalIgnoreCase))
                                {
                                    results.Add(file);
                                }
                            }
                        }
                        catch (UnauthorizedAccessException) { }
                        catch (PathTooLongException) { }
                    });

                    var fileContent = File.ReadAllText(results.FirstOrDefault() ?? string.Empty);
                    var systemPrompt =
                            "You are a technical writer producing developer documentation for a C# codebase. " +
                            "You will be given the full source code of a C# file. " +
                            "Write clear, accurate Markdown documentation describing this code for other developers. " +
                            "For each public type in the file: " +
                            "give a one-paragraph summary of its purpose and role in the codebase; " +
                            "document each public property (name, type, what it represents); " +
                            "document each public method (name, parameters, return value, and what it actually does " +
                            "based on its implementation, not just its name). " +
                            "Use level-2 headings (##) for each type and level-3 headings (###) for each member. " +
                            "Use fenced code blocks only for method signatures, not full implementations. " +
                            "Do not invent behavior that isn't in the code, and do not document private or internal members. " +
                            "Output only the Markdown document itself — no preamble, no explanation, no surrounding commentary.";
                    
                    _messageService.Publish(new SendToolPromptRequest(systemPrompt, fileContent));
                }
            }
        }
    }
}