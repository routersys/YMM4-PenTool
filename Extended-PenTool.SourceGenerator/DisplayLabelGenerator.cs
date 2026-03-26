using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ExtendedPenTool.SourceGenerator;

[Generator]
public sealed class DisplayLabelGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "ExtendedPenTool.SourceGenerator.DisplayLabelAttribute";

    private static readonly string AttributeSource = @"using System;
namespace ExtendedPenTool.SourceGenerator
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class DisplayLabelAttribute : Attribute
    {
        public DisplayLabelAttribute(string label)
        {
            Label = label;
        }
        public string Label { get; }
        public Type ResourceType { get; set; }
        public string ResourceName { get; set; }
    }
}
";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource("DisplayLabelAttribute.g.cs", SourceText.From(AttributeSource, Encoding.UTF8)));

        var enums = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is EnumDeclarationSyntax,
                transform: static (ctx, _) =>
                    ctx.SemanticModel.GetDeclaredSymbol((EnumDeclarationSyntax)ctx.Node) as INamedTypeSymbol)
            .Where(static s => s is not null && HasDisplayLabelAttribute(s))
            .Collect();

        context.RegisterSourceOutput(enums, Execute);
    }

    private static bool HasDisplayLabelAttribute(INamedTypeSymbol symbol)
    {
        foreach (var member in symbol.GetMembers())
        {
            if (member is IFieldSymbol field)
            {
                foreach (var attr in field.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() == AttributeFullName)
                        return true;
                }
            }
        }
        return false;
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<INamedTypeSymbol?> symbols)
    {
        foreach (var symbol in symbols)
        {
            if (symbol is null) continue;
            var source = GenerateExtensions(symbol);
            context.AddSource($"{symbol.Name}Extensions.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateExtensions(INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace.ToDisplayString();
        var name = symbol.Name;
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {ns}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal static partial class {name}Extensions");
        sb.AppendLine("    {");
        sb.AppendLine($"        public static string GetDisplayLabel(this {name} value) => value switch");
        sb.AppendLine("        {");

        foreach (var member in symbol.GetMembers())
        {
            if (member is not IFieldSymbol field) continue;

            foreach (var attr in field.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != AttributeFullName) continue;

                var resourceTypeArg = attr.NamedArguments
                    .FirstOrDefault(a => a.Key == "ResourceType").Value;
                var resourceNameArg = attr.NamedArguments
                    .FirstOrDefault(a => a.Key == "ResourceName").Value;

                string valueExpr;
                if (resourceTypeArg.Kind == TypedConstantKind.Type
                    && resourceTypeArg.Value is INamedTypeSymbol resourceType
                    && resourceNameArg.Value is string resourceName)
                {
                    valueExpr = $"global::{resourceType.ToDisplayString()}.{resourceName}";
                }
                else if (attr.ConstructorArguments.Length > 0
                    && attr.ConstructorArguments[0].Value is string label)
                {
                    valueExpr = $"\"{EscapeString(label)}\"";
                }
                else
                {
                    continue;
                }

                sb.AppendLine($"            {name}.{field.Name} => {valueExpr},");
            }
        }

        sb.AppendLine("            _ => string.Empty,");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeString(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
