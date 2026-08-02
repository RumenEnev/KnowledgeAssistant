namespace KnowledgeAssistant.Wpf.Models.Files.CSharp
{
    public class TypeInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Namespace { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty; // class, struct, interface, record

        public List<PropertyInfo> Properties { get; set; } = new();

        public List<MethodInfo> Methods { get; set; } = new();

        public string? DocComment { get; set; }
    }
}