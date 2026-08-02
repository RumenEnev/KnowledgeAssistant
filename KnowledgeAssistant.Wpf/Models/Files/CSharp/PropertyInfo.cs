namespace KnowledgeAssistant.Wpf.Models.Files.CSharp
{
    public class PropertyInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Modifiers { get; set; } = string.Empty;

        public bool HasGetter { get; set; }

        public bool HasSetter { get; set; }

        public string? DocComment { get; set; }
    }
}