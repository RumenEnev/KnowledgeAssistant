using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DocumentCreator.Services.CSharp;

public static class TypeExtractor
{
    public static List<TypeInfo> ExtractTypes(string sourceCode)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        var results = new List<TypeInfo>();
        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        foreach (var typeDecl in typeDeclarations)
        {
            var typeInfo = new TypeInfo
            {
                Name = typeDecl.Identifier.Text,
                Namespace = GetNamespace(typeDecl),
                Kind = GetTypeKind(typeDecl),
                DocComment = GetXmlDocComment(typeDecl)
            };

            // Properties
            foreach (var prop in typeDecl.Members.OfType<PropertyDeclarationSyntax>())
            {
                var accessors = prop.AccessorList?.Accessors
                    .Select(a => a.Keyword.Text)
                    .ToList() ?? new List<string>();

                typeInfo.Properties.Add(new PropertyInfo
                {
                    Name = prop.Identifier.Text,
                    Type = prop.Type.ToString(),
                    Modifiers = string.Join(" ", prop.Modifiers.Select(m => m.Text)),
                    HasGetter = accessors.Contains("get"),
                    HasSetter = accessors.Contains("set") || accessors.Contains("init"),
                    DocComment = GetXmlDocComment(prop)
                });
            }

            // Methods
            foreach (var method in typeDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                typeInfo.Methods.Add(new MethodInfo
                {
                    Name = method.Identifier.Text,
                    ReturnType = method.ReturnType.ToString(),
                    Modifiers = string.Join(" ", method.Modifiers.Select(m => m.Text)),
                    DocComment = GetXmlDocComment(method),
                    Parameters = method.ParameterList.Parameters
                        .Select(p => $"{p.Type} {p.Identifier.Text}")
                        .ToList(),
                });
            }

            results.Add(typeInfo);
        }

        return results;
    }

    private static string GetTypeKind(TypeDeclarationSyntax typeDecl) => typeDecl switch
    {
        RecordDeclarationSyntax => "record",
        ClassDeclarationSyntax => "class",
        StructDeclarationSyntax => "struct",
        InterfaceDeclarationSyntax => "interface",
        _ => "unknown"
    };

    private static string GetNamespace(SyntaxNode node)
    {
        // Handles both classic "namespace X { }" and modern file-scoped "namespace X;"
        var nsNode = node.Ancestors()
            .FirstOrDefault(a => a is NamespaceDeclarationSyntax || a is FileScopedNamespaceDeclarationSyntax);

        return nsNode switch
        {
            NamespaceDeclarationSyntax ns => ns.Name.ToString(),
            FileScopedNamespaceDeclarationSyntax fs => fs.Name.ToString(),
            _ => string.Empty
        };
    }

    private static string? GetXmlDocComment(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia()
            .Select(t => t.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        if (trivia == null)
        {
            return null;
        }

        var lines = trivia.Content
            .Select(x => x.ToFullString())
            .ToList();

        string raw = string.Concat(lines);
        var cleaned = raw.Split('\n')
            .Select(l => l.TrimStart().TrimStart('/').Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l));

        string result = string.Join(Environment.NewLine, cleaned);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}