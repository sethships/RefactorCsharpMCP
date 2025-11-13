using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Analyzes code selection to find the containing method and statements within a line range.
/// Responsible for discovering the extraction context within the syntax tree.
/// </summary>
internal class CodeSelectionAnalyzer
{
    /// <summary>
    /// Finds the method declaration that contains the specified line range.
    /// </summary>
    /// <param name="root">The syntax tree root to search</param>
    /// <param name="startLine">The starting line number (1-based)</param>
    /// <param name="endLine">The ending line number (1-based)</param>
    /// <returns>The containing method declaration, or null if not found</returns>
    public MethodDeclarationSyntax? FindContainingMethod(CompilationUnitSyntax root, int startLine, int endLine)
    {
        return root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(method =>
            {
                var methodSpan = method.GetLocation().GetLineSpan();
                return methodSpan.StartLinePosition.Line + 1 <= startLine &&
                       methodSpan.EndLinePosition.Line + 1 >= endLine;
            });
    }

    /// <summary>
    /// Finds all statements within the specified line range.
    /// </summary>
    /// <param name="method">The method to search within</param>
    /// <param name="startLine">The starting line number (1-based, inclusive)</param>
    /// <param name="endLine">The ending line number (1-based, inclusive)</param>
    /// <returns>List of statements within the line range</returns>
    public List<StatementSyntax> FindStatementsInLineRange(
        MethodDeclarationSyntax method,
        int startLine,
        int endLine)
    {
        if (method.Body == null) return new List<StatementSyntax>();

        return method.Body.Statements
            .Where(statement =>
            {
                var stmtSpan = statement.GetLocation().GetLineSpan();
                var stmtStart = stmtSpan.StartLinePosition.Line + 1;
                var stmtEnd = stmtSpan.EndLinePosition.Line + 1;

                // Statement is within or overlaps the line range
                return (stmtStart >= startLine && stmtStart <= endLine) ||
                       (stmtEnd >= startLine && stmtEnd <= endLine) ||
                       (stmtStart <= startLine && stmtEnd >= endLine);
            })
            .ToList();
    }
}
