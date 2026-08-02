namespace KnowledgeAssistant.Wpf.Models.Files.CSharp
{
    public class MethodInfo
    {
        public string Name { get; set; } = string.Empty;

        public string ReturnType { get; set; } = string.Empty;

        public string Modifiers { get; set; } = string.Empty;

        public List<string> Parameters { get; set; } = new();

        public string? DocComment { get; set; }
    }
}