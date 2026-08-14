namespace DocumentCreator.Services.CSharp;

public class MethodInfo
{
    public string Name { get; set; } = string.Empty;

    public string ReturnType { get; set; } = string.Empty;

    public string Modifiers { get; set; } = string.Empty;

    public List<string> Parameters { get; set; } = new();

    public string? DocComment { get; set; }

    public override string ToString()
    {
        var doc = string.IsNullOrWhiteSpace(DocComment) ? "" : DocComment + "\n";
        var parameters = string.Join(", ", Parameters);

        return $"{doc}{Modifiers} {ReturnType} {Name}({parameters})";
    }
}