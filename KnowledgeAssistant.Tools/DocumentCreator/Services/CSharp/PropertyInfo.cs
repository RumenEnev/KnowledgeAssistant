namespace DocumentCreator.Services.CSharp;

public class PropertyInfo
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Modifiers { get; set; } = string.Empty;

    public bool HasGetter { get; set; }

    public bool HasSetter { get; set; }

    public string? DocComment { get; set; }

    public override string ToString()
    {
        var doc = string.IsNullOrWhiteSpace(DocComment) ? "" : DocComment + "\n";
        var accessors = (HasGetter, HasSetter) switch
        {
            (true, true) => "{ get; set; }",
            (true, false) => "{ get; }",
            (false, true) => "{ set; }",
            _ => "{ }"
        };

        return $"{doc}{Modifiers} {Type} {Name} {accessors}";
    }
}