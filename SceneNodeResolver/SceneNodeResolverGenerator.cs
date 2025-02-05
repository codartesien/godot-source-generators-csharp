namespace Codartesien.SourceGenerators.SceneNodeResolver;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[Generator]
public class SceneNodeResolverGenerator : ISourceGenerator
{
    /// <summary>
    /// Set this to true to dump the generated code to a file for debugging.
    /// </summary>
    private readonly bool _debugDump = false;
    
    /// <summary>
    /// Change this to the path where you want to dump the generated code for debugging.
    /// </summary>
    private const string DEBUG_DUMP_PATH = "/tmp/source_generator_log.txt";
        
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new SceneNodeSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SceneNodeSyntaxReceiver receiver) {
            return;
        }

        if (_debugDump) { 
            System.IO.File.Delete(DEBUG_DUMP_PATH);
            System.IO.File.AppendAllText(DEBUG_DUMP_PATH, "=== GENERATION RUN ===\n");
        }

        foreach (var classNode in receiver.CandidateClasses) {
            var namespaceName = classNode.Parent is BaseNamespaceDeclarationSyntax namespaceNode ? namespaceNode.Name.ToString() : "Global";
            var className = classNode.Identifier.Text;
            var uniqueHintName = $"{namespaceName}_{className}_SceneNodeResolver.g";

            var sourceCode = GenerateClass(classNode);

            if (_debugDump) {
                System.IO.File.AppendAllText(DEBUG_DUMP_PATH, $"=== {uniqueHintName} ===\n{sourceCode}\n====================\n");
            }
            context.AddSource(uniqueHintName, SourceText.From(sourceCode, Encoding.UTF8));
        }
    }

    private static string GenerateClass(ClassDeclarationSyntax classNode)
    {
        var namespaceName = classNode.Parent is BaseNamespaceDeclarationSyntax ns ? ns.Name.ToString() : "Global";
        var className = classNode.Identifier.Text;

        // Retrieve all using directives from the original file
        var usingsText = string.Join("\n", classNode.SyntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.ToString())
            .Distinct());

        var fields = classNode.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.AttributeLists.Any(attrList =>
                attrList.Attributes.Any(attr => attr.Name.ToString() == "SceneNode")))
            .Select(f => (
                Type: f.Declaration.Type.ToString(),
                Name: f.Declaration.Variables.First().Identifier.Text,
                NodePath: f.AttributeLists
                    .SelectMany(a => a.Attributes)
                    .Where(attr => attr.Name.ToString() == "SceneNode")
                    .Select(attr => attr.ArgumentList?.Arguments.First().ToString().Trim('"'))
                    .FirstOrDefault() ?? ""
            ))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine(usingsText);
        sb.AppendLine("#pragma warning disable CS0105 // Disable warning about redundant using directive");
        sb.AppendLine("using Codartesien.SourceGenerators.SceneNodeResolver;");
        sb.AppendLine("#pragma warning restore CS0105");
        sb.AppendLine();
        sb.AppendLine($"public partial class {className} : ISceneNodeResolver");
        sb.AppendLine("{");
        sb.AppendLine("#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword");
        sb.AppendLine("    public new void ResolveNodes()");
        sb.AppendLine("    {");

        foreach (var field in fields)
        {
            sb.AppendLine($"        this.{field.Name} = this.GetNode<{field.Type}>(\"{field.NodePath}\");");
        }

        sb.AppendLine("    }");
        sb.AppendLine("#pragma warning restore CS0109");
        sb.AppendLine("}");

        return sb.ToString();
    }
}

/// <summary>
/// Syntax Receiver to collect classes that have the [SceneNode] attribute.
/// </summary>
internal class SceneNodeSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is ClassDeclarationSyntax classDecl 
            && classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
            && !classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
            && !classDecl.SyntaxTree.FilePath.Contains("addons/")
            && classDecl.Members
                .OfType<FieldDeclarationSyntax>()
                .Any(f => f.AttributeLists
                    .SelectMany(a => a.Attributes)
                    .Any(attr => attr.Name.ToString() == "SceneNode")))
        {
            CandidateClasses.Add(classDecl);
        }
    }
}
