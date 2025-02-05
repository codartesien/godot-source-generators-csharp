namespace Codartesien.SourceGenerators.DependencyResolver;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[Generator]
public class DependencyResolverGenerator : ISourceGenerator
{
    /// <summary>
    /// Set this to true to dump the generated code to a file for debugging.
    /// </summary>
    private readonly bool _debugDump = false;
    
    /// <summary>
    /// Change this to the path where you want to dump the generated code for debugging.
    /// </summary>
    private const string DEBUG_DUMP_PATH = "/tmp/dependency_source_generator_log.txt";
        
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new DependencySyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not DependencySyntaxReceiver receiver) {
            return;
        }

        if (_debugDump) { 
            System.IO.File.Delete(DEBUG_DUMP_PATH);
            System.IO.File.AppendAllText(DEBUG_DUMP_PATH, "=== GENERATION RUN ===\n");
        }
        

        foreach (var classNode in receiver.CandidateClasses) {
            var namespaceName = classNode.Parent is BaseNamespaceDeclarationSyntax namespaceNode ? namespaceNode.Name.ToString() : "Global";
            var className = classNode.Identifier.Text;
            var uniqueHintName = $"{namespaceName}_{className}_DependencyResolver.g";
            var model = context.Compilation.GetSemanticModel(classNode.SyntaxTree);

            var sourceCode = GenerateClass(classNode, model, context.Compilation);

            if (_debugDump) {
                System.IO.File.AppendAllText(DEBUG_DUMP_PATH, $"=== {uniqueHintName} ===\n{sourceCode}\n====================\n");
            }
            context.AddSource(uniqueHintName, SourceText.From(sourceCode, Encoding.UTF8));
        }
    }

    private static string GenerateClass(ClassDeclarationSyntax classNode, SemanticModel model, Compilation compilation)
    {
        var namespaceName = classNode.Parent is BaseNamespaceDeclarationSyntax ns ? ns.Name.ToString() : "Global";
        var className = classNode.Identifier.Text;

        // Retrieve all using directives from the original file
        var usings = GetUsings(classNode, model);

        var fields = new List<(string Type, string Name, string NodePath)>();
        var classesLookedAt = new List<string>();

        var currentClass = classNode;
        while (currentClass != null)
        {
            var currentClassUsings = GetUsings(currentClass, model);
            usings.AddRange(currentClassUsings);
            
            var currentClassName = currentClass.Identifier.Text;
            classesLookedAt.Add(currentClassName);
            var classFields = currentClass.Members
                .OfType<FieldDeclarationSyntax>()
                .Where(f => f.AttributeLists.Any(attrList =>
                    attrList.Attributes.Any(attr => attr.Name.ToString() == "InjectDependency")))
                .Select(f => (
                    Type: f.Declaration.Type.ToString(),
                    Name: f.Declaration.Variables.First().Identifier.Text,
                    NodePath: f.AttributeLists
                        .SelectMany(a => a.Attributes)
                        .Where(attr => attr.Name.ToString() == "InjectDependency")
                        .Select(attr => attr.ArgumentList?.Arguments.First().ToString().Trim('"'))
                        .FirstOrDefault() ?? ""
                ))
                .ToList();

            fields.AddRange(classFields);

            // Récupérer la classe parente
            var baseType = currentClass.BaseList?.Types.FirstOrDefault()?.Type;
            if (baseType != null)
            {
                var baseSymbol = model.GetSymbolInfo(baseType).Symbol as INamedTypeSymbol;
                if (baseSymbol != null)
                {
                    var parentSyntaxRef = baseSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                    if (parentSyntaxRef != null)
                    {
                        var parentSyntaxTree = parentSyntaxRef.SyntaxTree;
                        var parentModel = compilation.GetSemanticModel(parentSyntaxTree);

                        currentClass = parentSyntaxRef.GetSyntax() as ClassDeclarationSyntax;
                        model = parentModel; // 🔹 Mise à jour du modèle sémantique
                    }
                    else
                    {
                        currentClass = null;
                    }
                }
            }
            else
            {
                currentClass = null; // Si pas de classe parente, on stoppe
            }
        }
        
        System.IO.File.AppendAllText(DEBUG_DUMP_PATH, $"Found {fields.Count} fields for class {className}: {string.Join(", ", fields.Select(f => f.Name))}\n");
        System.IO.File.AppendAllText(DEBUG_DUMP_PATH, $"Classes looked at: {string.Join(", ", classesLookedAt)}\n");
        

        var sb = new StringBuilder();
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();
        sb.AppendLine(string.Join("\n", usings.Distinct()));
        sb.AppendLine("#pragma warning disable CS0105 // Disable warning about redundant using directive");
        sb.AppendLine("using Codartesien.SourceGenerators.DependencyResolver;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("#pragma warning restore CS0105");
        sb.AppendLine();
        sb.AppendLine($"public partial class {className} : IDependencyResolver");
        sb.AppendLine("{");
        sb.AppendLine("#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword");
        sb.AppendLine("    public new void ResolveDependencies()");
        sb.AppendLine("    {");

        foreach (var field in fields) 
        {
            sb.AppendLine($"        this.{field.Name} = GetTree().Root.GetChildren().OfType<{field.Type}>().FirstOrDefault();");
        }

        sb.AppendLine("    }");
        sb.AppendLine("#pragma warning restore CS0109");
        sb.AppendLine("}");

        return sb.ToString();
    }
    
    private static List<string> GetUsings(ClassDeclarationSyntax classNode, SemanticModel model)
    {
        return classNode.SyntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u =>
            {
                var symbolInfo = model.GetSymbolInfo(u.Name);
                if (symbolInfo.Symbol is INamespaceSymbol namespaceSymbol)
                {
                    return $"using {namespaceSymbol.ToDisplayString()};"; // Convert in full namespace
                }
                return u.ToString(); // Keep the original using directive
            })
            .Distinct()
            .ToList();
    }
}

/// <summary>
/// Syntax Receiver to collect classes that have the [InjectDependency] attribute.
/// </summary>
internal class DependencySyntaxReceiver : ISyntaxReceiver
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
                    .Any(attr => attr.Name.ToString() == "InjectDependency")))
        {
            CandidateClasses.Add(classDecl);
        }
    }
}
