namespace DocumentCreator.Services.CSharp;

public class TypeInfo
{
    public string Name { get; set; } = string.Empty;

    public string Namespace { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty; // class, struct, interface, record

    public List<PropertyInfo> Properties { get; set; } = new();

    public List<MethodInfo> Methods { get; set; } = new();

    public string? DocComment { get; set; }

    public override string ToString()
    {
        var doc = string.IsNullOrWhiteSpace(DocComment) ? "" : DocComment + "\n";
        return $"{doc}// namespace {Namespace}\npublic {Kind} {Name}";
    }
}