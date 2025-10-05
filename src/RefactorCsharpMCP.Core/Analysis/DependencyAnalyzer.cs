using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Analysis;

/// <summary>
/// Analyzes dependencies between code elements.
/// </summary>
public class DependencyAnalyzer
{
    /// <summary>
    /// Analyzes method dependencies within a class.
    /// </summary>
    /// <param name="sourceCode">The source code to analyze.</param>
    /// <param name="className">The class name to analyze.</param>
    /// <returns>A dictionary mapping method names to their dependencies.</returns>
    public Dictionary<string, MethodDependencies> AnalyzeMethodDependencies(string sourceCode, string className)
    {
        var result = new Dictionary<string, MethodDependencies>();

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot();

            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration == null)
            {
                return result;
            }

            var methods = classDeclaration.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .ToList();

            foreach (var method in methods)
            {
                var dependencies = new MethodDependencies
                {
                    MethodName = method.Identifier.Text,
                    FieldsAccessed = new List<string>(),
                    MethodsCalled = new List<string>(),
                    ParameterTypes = new List<string>()
                };

                // Analyze field access
                var identifiers = method.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Select(i => i.Identifier.Text)
                    .Distinct()
                    .ToList();

                var classFields = classDeclaration.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables)
                    .Select(v => v.Identifier.Text)
                    .ToHashSet();

                dependencies.FieldsAccessed = identifiers
                    .Where(i => classFields.Contains(i))
                    .ToList();

                // Analyze method calls
                var invocations = method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .ToList();

                foreach (var invocation in invocations)
                {
                    var methodName = ExtractMethodName(invocation);
                    if (methodName != null && methodName != method.Identifier.Text)
                    {
                        dependencies.MethodsCalled.Add(methodName);
                    }
                }

                dependencies.MethodsCalled = dependencies.MethodsCalled.Distinct().ToList();

                // Analyze parameters
                dependencies.ParameterTypes = method.ParameterList.Parameters
                    .Select(p => p.Type?.ToString() ?? "unknown")
                    .ToList();

                result[method.Identifier.Text] = dependencies;
            }
        }
        catch
        {
            // Return empty result on error
        }

        return result;
    }

    /// <summary>
    /// Analyzes field usage within a class.
    /// </summary>
    /// <param name="sourceCode">The source code to analyze.</param>
    /// <param name="className">The class name to analyze.</param>
    /// <returns>A dictionary mapping field names to their usage information.</returns>
    public Dictionary<string, FieldUsage> AnalyzeFieldUsage(string sourceCode, string className)
    {
        var result = new Dictionary<string, FieldUsage>();

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot();

            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration == null)
            {
                return result;
            }

            var fields = classDeclaration.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .ToList();

            foreach (var field in fields)
            {
                var fieldName = field.Identifier.Text;
                var usage = new FieldUsage
                {
                    FieldName = fieldName,
                    UsedInMethods = new List<string>(),
                    IsReadOnly = false,
                    HasInitializer = field.Initializer != null
                };

                // Check if readonly
                var fieldDecl = field.FirstAncestorOrSelf<FieldDeclarationSyntax>();
                if (fieldDecl != null)
                {
                    usage.IsReadOnly = fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
                }

                // Find methods that use this field
                var methods = classDeclaration.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .ToList();

                foreach (var method in methods)
                {
                    var usesField = method.DescendantNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Any(i => i.Identifier.Text == fieldName);

                    if (usesField)
                    {
                        usage.UsedInMethods.Add(method.Identifier.Text);
                    }
                }

                result[fieldName] = usage;
            }
        }
        catch
        {
            // Return empty result on error
        }

        return result;
    }

    private string? ExtractMethodName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is IdentifierNameSyntax identifierName)
        {
            return identifierName.Identifier.Text;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Name is IdentifierNameSyntax memberName)
            {
                return memberName.Identifier.Text;
            }
        }

        return null;
    }
}

/// <summary>
/// Represents dependencies for a method.
/// </summary>
public class MethodDependencies
{
    public required string MethodName { get; set; }
    public required List<string> FieldsAccessed { get; set; }
    public required List<string> MethodsCalled { get; set; }
    public required List<string> ParameterTypes { get; set; }
}

/// <summary>
/// Represents usage information for a field.
/// </summary>
public class FieldUsage
{
    public required string FieldName { get; set; }
    public required List<string> UsedInMethods { get; set; }
    public required bool IsReadOnly { get; set; }
    public required bool HasInitializer { get; set; }
}
