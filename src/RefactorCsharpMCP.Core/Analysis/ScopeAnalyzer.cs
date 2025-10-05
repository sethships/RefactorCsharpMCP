using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Analysis;

/// <summary>
/// Analyzes variable scope and accessibility in code.
/// </summary>
public class ScopeAnalyzer
{
    /// <summary>
    /// Analyzes variables used within a code block.
    /// </summary>
    /// <param name="sourceCode">The source code to analyze.</param>
    /// <param name="className">The class name containing the code.</param>
    /// <param name="methodName">The method name to analyze.</param>
    /// <param name="startLine">The starting line number (1-based).</param>
    /// <param name="endLine">The ending line number (1-based).</param>
    /// <returns>Information about variables used in the scope.</returns>
    public ScopeInfo AnalyzeScope(
        string sourceCode,
        string className,
        string methodName,
        int startLine,
        int endLine)
    {
        var scopeInfo = new ScopeInfo
        {
            LocalVariables = new List<string>(),
            ParameterVariables = new List<string>(),
            FieldVariables = new List<string>(),
            ExternalMethodCalls = new List<string>()
        };

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot();

            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration == null)
            {
                return scopeInfo;
            }

            var methodDeclaration = classDeclaration.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName);

            if (methodDeclaration == null)
            {
                return scopeInfo;
            }

            // Get all statements in the specified range
            var statementsInRange = methodDeclaration.DescendantNodes()
                .OfType<StatementSyntax>()
                .Where(s =>
                {
                    var span = s.GetLocation().GetLineSpan();
                    var lineStart = span.StartLinePosition.Line + 1;
                    var lineEnd = span.EndLinePosition.Line + 1;
                    return lineStart >= startLine && lineEnd <= endLine;
                })
                .ToList();

            if (!statementsInRange.Any())
            {
                return scopeInfo;
            }

            // Get class fields
            var classFields = classDeclaration.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .Select(v => v.Identifier.Text)
                .ToHashSet();

            // Get method parameters
            var parameters = methodDeclaration.ParameterList.Parameters
                .Select(p => p.Identifier.Text)
                .ToHashSet();

            // Get local variables declared before the selection
            var localVariables = methodDeclaration.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v =>
                {
                    var span = v.GetLocation().GetLineSpan();
                    var line = span.StartLinePosition.Line + 1;
                    return line < startLine;
                })
                .Select(v => v.Identifier.Text)
                .ToHashSet();

            // Analyze identifiers used in the range
            var identifiersUsed = statementsInRange
                .SelectMany(s => s.DescendantNodes())
                .OfType<IdentifierNameSyntax>()
                .Select(i => i.Identifier.Text)
                .Distinct()
                .ToList();

            foreach (var identifier in identifiersUsed)
            {
                if (classFields.Contains(identifier))
                {
                    scopeInfo.FieldVariables.Add(identifier);
                }
                else if (parameters.Contains(identifier))
                {
                    scopeInfo.ParameterVariables.Add(identifier);
                }
                else if (localVariables.Contains(identifier))
                {
                    scopeInfo.LocalVariables.Add(identifier);
                }
            }

            // Analyze method calls
            var invocations = statementsInRange
                .SelectMany(s => s.DescendantNodes())
                .OfType<InvocationExpressionSyntax>()
                .ToList();

            foreach (var invocation in invocations)
            {
                var methodNameCalled = ExtractMethodName(invocation);
                if (methodNameCalled != null && methodNameCalled != methodName)
                {
                    scopeInfo.ExternalMethodCalls.Add(methodNameCalled);
                }
            }

            scopeInfo.ExternalMethodCalls = scopeInfo.ExternalMethodCalls.Distinct().ToList();
        }
        catch
        {
            // Return empty result on error
        }

        return scopeInfo;
    }

    /// <summary>
    /// Determines if a code block can be safely extracted based on scope analysis.
    /// </summary>
    /// <param name="sourceCode">The source code to analyze.</param>
    /// <param name="className">The class name containing the code.</param>
    /// <param name="methodName">The method name to analyze.</param>
    /// <param name="startLine">The starting line number (1-based).</param>
    /// <param name="endLine">The ending line number (1-based).</param>
    /// <returns>Result indicating if extraction is safe and any issues found.</returns>
    public ExtractionAnalysis AnalyzeExtraction(
        string sourceCode,
        string className,
        string methodName,
        int startLine,
        int endLine)
    {
        var analysis = new ExtractionAnalysis
        {
            CanExtract = true,
            Issues = new List<string>(),
            VariablesNeeded = new List<string>(),
            ReturnType = "void"
        };

        try
        {
            var scopeInfo = AnalyzeScope(sourceCode, className, methodName, startLine, endLine);

            // Variables that need to be passed as parameters
            analysis.VariablesNeeded.AddRange(scopeInfo.LocalVariables);
            analysis.VariablesNeeded.AddRange(scopeInfo.ParameterVariables);

            // Check for field usage - may need to pass as parameters or keep in same class
            if (scopeInfo.FieldVariables.Any())
            {
                analysis.Issues.Add($"Uses {scopeInfo.FieldVariables.Count} class field(s): {string.Join(", ", scopeInfo.FieldVariables)}");
            }

            // Check for external method calls
            if (scopeInfo.ExternalMethodCalls.Any())
            {
                analysis.Issues.Add($"Calls {scopeInfo.ExternalMethodCalls.Count} other method(s): {string.Join(", ", scopeInfo.ExternalMethodCalls)}");
            }
        }
        catch
        {
            analysis.CanExtract = false;
            analysis.Issues.Add("Error analyzing code for extraction");
        }

        return analysis;
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
/// Information about variables in a scope.
/// </summary>
public class ScopeInfo
{
    public required List<string> LocalVariables { get; set; }
    public required List<string> ParameterVariables { get; set; }
    public required List<string> FieldVariables { get; set; }
    public required List<string> ExternalMethodCalls { get; set; }
}

/// <summary>
/// Analysis result for code extraction feasibility.
/// </summary>
public class ExtractionAnalysis
{
    public required bool CanExtract { get; set; }
    public required List<string> Issues { get; set; }
    public required List<string> VariablesNeeded { get; set; }
    public required string ReturnType { get; set; }
}
