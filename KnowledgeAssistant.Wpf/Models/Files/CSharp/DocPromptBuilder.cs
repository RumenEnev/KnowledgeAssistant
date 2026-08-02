using KnowledgeAssistant.Wpf.Models.Files.CSharp;
using System.Text;

public static class DocPromptBuilder
{
    public static string BuildCSharpDocumentationContext(List<TypeInfo> types)
    {
        var stringBuilder = new StringBuilder();
        foreach (var type in types)
        {
            stringBuilder.AppendLine($"// {type.Kind} {type.Namespace}.{type.Name}");
            if (!string.IsNullOrWhiteSpace(type.DocComment))
            {
                stringBuilder.AppendLine($"// Existing doc: {type.DocComment}");
            }

            stringBuilder.AppendLine($"public {type.Kind} {type.Name}");
            stringBuilder.AppendLine("{");
            foreach (var p in type.Properties)
            {
                if (!string.IsNullOrWhiteSpace(p.DocComment))
                {
                    stringBuilder.AppendLine($"    // {p.DocComment}");
                }

                stringBuilder.AppendLine($"    public {p.Type} {p.Name} {{ {(p.HasGetter ? "get;" : "")} {(p.HasSetter ? "set;" : "")} }}");
            }

            foreach (var m in type.Methods)
            {
                if (!string.IsNullOrWhiteSpace(m.DocComment))
                {
                    stringBuilder.AppendLine($"    // {m.DocComment}");
                }

                stringBuilder.AppendLine($"    public {m.ReturnType} {m.Name}({string.Join(", ", m.Parameters)});");
            }

            stringBuilder.AppendLine("}");
            stringBuilder.AppendLine();
        }

        return stringBuilder.ToString();
    }
}