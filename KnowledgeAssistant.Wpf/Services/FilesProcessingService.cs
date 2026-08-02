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
                    var types = TypeExtractor.ExtractTypes(fileContent);
                    //_messageService.Publish(new SendPromptRequest(request.Prompt, _conversation?.SelectedModel ?? _currentModel, "tool", null));
                }
            }
        }
    }
}