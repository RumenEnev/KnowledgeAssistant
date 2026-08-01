using KnowledgeAssistant.Wpf.Messages.RepositoriesManagement;
using KnowledgeAssistant.Wpf.Models.Files;
using MessageServices;
using System.IO;

namespace KnowledgeAssistant.Wpf.Services
{
    public class FilesProcessingService : IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;

        public FilesProcessingService(MessageService messageService)
        {
            _messageService = messageService;
        }

        private async Task<LocatedFile?> LocateFileAsync(string fileHint, CancellationToken cancellationToken)
        {
            var repositories = await _messageService.RequestAsync<RepositoriesReceivedEvent>(new GetRepositoriesRequest());
            var normalizedHint = fileHint.Replace('\\', '/');
            var hintFileName = Path.GetFileName(normalizedHint);
            var candidates = new List<LocatedFile>();
            //foreach (var repository in repositories.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            //{
            //    if (!Directory.Exists(repository.RootPath))
            //    {
            //        continue;
            //    }

            //    foreach (var fullPath in EnumerateFilesSafely(repository.RootPath))
            //    {
            //        cancellationToken.ThrowIfCancellationRequested();

            //        var relativePath = Path.GetRelativePath(repository.RootPath, fullPath).Replace('\\', '/');
            //        var isMatch = string.Equals(relativePath, normalizedHint, StringComparison.OrdinalIgnoreCase)
            //            || relativePath.EndsWith("/" + normalizedHint, StringComparison.OrdinalIgnoreCase)
            //            || string.Equals(Path.GetFileName(fullPath), hintFileName, StringComparison.OrdinalIgnoreCase);

            //        if (isMatch)
            //        {
            //            candidates.Add(new LocatedFile(repository, relativePath, fullPath));
            //        }
            //    }
            //}

            var exactMatch = candidates.FirstOrDefault(c => string.Equals(c.RelativeFilePath, normalizedHint, StringComparison.OrdinalIgnoreCase));
            return exactMatch ?? candidates.FirstOrDefault();
        }
    }
}