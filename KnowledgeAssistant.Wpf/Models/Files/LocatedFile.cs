using KnowledgeAssistant.Domain;
using System.IO;

namespace KnowledgeAssistant.Wpf.Models.Files
{
    public sealed record LocatedFile(SourceRepository Repository, string RelativeFilePath, string FullPath)
    {
        public string FileName => Path.GetFileName(FullPath);
    }
}