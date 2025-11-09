using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RefactorCsharpMCP.Core.Utilities.Symbols;

/// <summary>
/// Provides position-based symbol resolution in C# code.
/// Resolves symbols at specific line/column positions using Roslyn semantic analysis.
/// </summary>
/// <remarks>
/// This class was extracted from SymbolResolutionHelper.cs as part of Sprint 3 decomposition (Issue #90).
/// It focuses solely on position-to-symbol resolution, maintaining SyntaxTree identity for downstream operations.
/// </remarks>
public class PositionBasedResolver
{
    /// <summary>
    /// Result of symbol resolution at a specific position.
    /// </summary>
    public class SymbolResolutionResult
    {
        /// <summary>
        /// Indicates whether a symbol was found at the position.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// The resolved symbol, if found.
        /// </summary>
        public ISymbol? Symbol { get; init; }

        /// <summary>
        /// The syntax node at the position.
        /// </summary>
        public SyntaxNode? Node { get; init; }

        /// <summary>
        /// Error message if resolution failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Creates a successful resolution result.
        /// </summary>
        public static SymbolResolutionResult Successful(ISymbol symbol, SyntaxNode node)
        {
            return new SymbolResolutionResult
            {
                Success = true,
                Symbol = symbol,
                Node = node,
                ErrorMessage = null
            };
        }

        /// <summary>
        /// Creates a failed resolution result.
        /// </summary>
        public static SymbolResolutionResult Failed(string errorMessage)
        {
            return new SymbolResolutionResult
            {
                Success = false,
                Symbol = null,
                Node = null,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Gets the symbol at a specific line and column position in source code.
    /// </summary>
    /// <param name="sourceCode">The source code to analyze.</param>
    /// <param name="lineNumber">1-based line number.</param>
    /// <param name="columnNumber">1-based column number.</param>
    /// <returns>A result containing the symbol at the position, or an error.</returns>
    public SymbolResolutionResult GetSymbolAtPosition(string sourceCode, int lineNumber, int columnNumber)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return SymbolResolutionResult.Failed("Source code cannot be empty.");
        }

        if (lineNumber < 1 || columnNumber < 1)
        {
            return SymbolResolutionResult.Failed($"Invalid position: line {lineNumber}, column {columnNumber}. Must be 1-based.");
        }

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot();

            // Convert 1-based line/column to 0-based for Roslyn
            var position = GetTextPosition(syntaxTree, lineNumber - 1, columnNumber - 1);
            if (position == null)
            {
                return SymbolResolutionResult.Failed($"Position line {lineNumber}, column {columnNumber} is out of range.");
            }

            // Find the syntax node at this position
            var node = root.FindNode(new TextSpan(position.Value, 0));
            if (node == null)
            {
                return SymbolResolutionResult.Failed($"No syntax node found at line {lineNumber}, column {columnNumber}.");
            }

            // Create compilation and semantic model
            var compilation = CSharpCompilation.Create("temp")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Try to get symbol info
            var symbolInfo = semanticModel.GetSymbolInfo(node);
            var symbol = symbolInfo.Symbol;

            if (symbol == null)
            {
                // Try getting declared symbol if it's a declaration
                symbol = semanticModel.GetDeclaredSymbol(node);
            }

            if (symbol == null)
            {
                return SymbolResolutionResult.Failed($"No symbol found at line {lineNumber}, column {columnNumber}.");
            }

            return SymbolResolutionResult.Successful(symbol, node);
        }
        catch (Exception ex)
        {
            // Sanitize exception message to avoid leaking internal details
            var errorCategory = ex switch
            {
                ArgumentOutOfRangeException => "InvalidPosition",
                ArgumentException => "InvalidArgument",
                InvalidOperationException => "InvalidState",
                _ => "InternalError"
            };
            return SymbolResolutionResult.Failed($"Error resolving symbol ({errorCategory}). Verify source code syntax and position.");
        }
    }

    /// <summary>
    /// Gets the symbol at a specific line and column position using an existing semantic model.
    /// This overload maintains SyntaxTree identity for operations requiring consistent compilation context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>SyntaxTree Identity Requirements:</strong>
    /// </para>
    /// <para>
    /// Roslyn's semantic analysis relies on object identity for SyntaxTree instances. When you create
    /// a SemanticModel from a Compilation, that model is bound to specific SyntaxTree objects.
    /// Operations like finding references, analyzing symbols, or detecting conflicts must use the
    /// SAME SyntaxTree instances throughout the entire refactoring operation.
    /// </para>
    /// <para>
    /// <strong>When to use this overload:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>You already have a parsed SyntaxTree from a previous operation</item>
    /// <item>You need to find references after resolving a symbol</item>
    /// <item>You're implementing a position-based refactoring (e.g., RenameSymbol)</item>
    /// <item>You want to leverage compilation caching for performance</item>
    /// </list>
    /// <para>
    /// <strong>When to use the string-based overload:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>Standalone symbol lookup without existing compilation context</item>
    /// <item>Quick diagnostic or validation checks</item>
    /// <item>You don't need to perform further operations on the symbol</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Canonical pattern for position-based refactorings:
    /// var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
    /// var compilation = CreateCompilation(syntaxTree);  // Leverages cache
    /// var semanticModel = compilation.GetSemanticModel(syntaxTree);
    ///
    /// // Use THIS overload to maintain SyntaxTree identity
    /// var symbolResult = resolver.GetSymbolAtPosition(semanticModel, syntaxTree, line, column);
    ///
    /// // Now find references using the SAME compilation
    /// var references = locator.GetAllReferences(symbolResult.Symbol, compilation);
    /// </code>
    /// </example>
    /// <param name="semanticModel">The semantic model to use for symbol resolution (must not be null).</param>
    /// <param name="syntaxTree">The syntax tree containing the position (must match semantic model's tree).</param>
    /// <param name="lineNumber">1-based line number.</param>
    /// <param name="columnNumber">1-based column number.</param>
    /// <returns>A result containing the symbol at the position, or an error.</returns>
    public SymbolResolutionResult GetSymbolAtPosition(
        SemanticModel semanticModel,
        SyntaxTree syntaxTree,
        int lineNumber,
        int columnNumber)
    {
        if (semanticModel == null)
        {
            return SymbolResolutionResult.Failed("Semantic model must not be null.");
        }

        if (syntaxTree == null)
        {
            return SymbolResolutionResult.Failed("Syntax tree must not be null.");
        }

        if (lineNumber < 1 || columnNumber < 1)
        {
            return SymbolResolutionResult.Failed($"Invalid position: line {lineNumber}, column {columnNumber}. Must be 1-based.");
        }

        try
        {
            var root = syntaxTree.GetRoot();

            // Convert 1-based line/column to 0-based for Roslyn
            var position = GetTextPosition(syntaxTree, lineNumber - 1, columnNumber - 1);
            if (position == null)
            {
                return SymbolResolutionResult.Failed($"Position line {lineNumber}, column {columnNumber} is out of range.");
            }

            // Find the syntax node at this position
            var node = root.FindNode(new TextSpan(position.Value, 0));
            if (node == null)
            {
                return SymbolResolutionResult.Failed($"No syntax node found at line {lineNumber}, column {columnNumber}.");
            }

            // Use provided semantic model (maintains SyntaxTree identity)
            var symbolInfo = semanticModel.GetSymbolInfo(node);
            var symbol = symbolInfo.Symbol;

            if (symbol == null)
            {
                // Try getting declared symbol if it's a declaration
                symbol = semanticModel.GetDeclaredSymbol(node);
            }

            if (symbol == null)
            {
                return SymbolResolutionResult.Failed($"No symbol found at line {lineNumber}, column {columnNumber}.");
            }

            return SymbolResolutionResult.Successful(symbol, node);
        }
        catch (Exception ex)
        {
            // Sanitize exception message to avoid leaking internal details
            var errorCategory = ex switch
            {
                ArgumentOutOfRangeException => "InvalidPosition",
                ArgumentException => "InvalidArgument",
                InvalidOperationException => "InvalidState",
                _ => "InternalError"
            };
            return SymbolResolutionResult.Failed($"Error resolving symbol ({errorCategory}). Verify source code syntax and position.");
        }
    }

    /// <summary>
    /// Converts 0-based line and column to a text position in the syntax tree.
    /// </summary>
    private int? GetTextPosition(SyntaxTree syntaxTree, int line, int column)
    {
        try
        {
            var text = syntaxTree.GetText();
            var linePosition = new LinePosition(line, column);
            return text.Lines.GetPosition(linePosition);
        }
        catch
        {
            return null;
        }
    }
}
