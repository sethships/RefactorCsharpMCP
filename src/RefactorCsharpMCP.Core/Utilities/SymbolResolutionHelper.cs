using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Utilities.Symbols;

namespace RefactorCsharpMCP.Core.Utilities;

/// <summary>
/// Facade for symbol resolution utilities in C# code.
/// Provides a simplified API for position-based symbol resolution, conflict detection,
/// scope analysis, and reference finding by delegating to specialized classes.
/// </summary>
/// <remarks>
/// <para>
/// This class was refactored to a facade pattern as part of Sprint 3 decomposition (Issue #90).
/// The original 643-line class was split into 5 focused classes:
/// </para>
/// <list type="bullet">
/// <item><see cref="PositionBasedResolver"/>: Position-to-symbol resolution</item>
/// <item><see cref="ConflictDetector"/>: Symbol name conflict detection</item>
/// <item><see cref="ScopeAnalyzer"/>: Symbol scope and accessibility analysis</item>
/// <item><see cref="ReferenceLocator"/>: Finding references across compilation</item>
/// <item><see cref="SymbolResolutionHelper"/>: Facade for simplified API</item>
/// </list>
/// <para>
/// <strong>When to use the facade vs specialized classes:</strong>
/// </para>
/// <list type="bullet">
/// <item>Use <strong>SymbolResolutionHelper</strong> (this facade) for simple, one-off operations</item>
/// <item>Use specialized classes directly for fine-grained control or performance optimization</item>
/// <item>Example: For batch conflict detection, inject <see cref="ConflictDetector"/> directly to avoid facade overhead</item>
/// </list>
/// </remarks>
public class SymbolResolutionHelper
{
    private readonly PositionBasedResolver _positionResolver;
    private readonly ConflictDetector _conflictDetector;
    private readonly ScopeAnalyzer _scopeAnalyzer;
    private readonly ReferenceLocator _referenceLocator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolResolutionHelper"/> class.
    /// Creates instances of all specialized symbol utility classes.
    /// </summary>
    /// <remarks>
    /// All specialized classes have parameterless constructors with no I/O or external dependencies,
    /// guaranteeing exception-free initialization. Constructor cannot fail under normal conditions.
    /// </remarks>
    public SymbolResolutionHelper()
    {
        _positionResolver = new PositionBasedResolver();
        _conflictDetector = new ConflictDetector();
        _scopeAnalyzer = new ScopeAnalyzer();
        _referenceLocator = new ReferenceLocator();
    }

    /// <summary>
    /// Result of symbol resolution at a specific position.
    /// </summary>
    /// <remarks>
    /// Forwarded from <see cref="PositionBasedResolver.SymbolResolutionResult"/> for backward compatibility.
    /// </remarks>
    public class SymbolResolutionResult : PositionBasedResolver.SymbolResolutionResult
    {
        /// <summary>
        /// Creates a successful resolution result.
        /// </summary>
        public static new SymbolResolutionResult Successful(ISymbol symbol, SyntaxNode node)
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
        public static new SymbolResolutionResult Failed(string errorMessage)
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
    /// Result of symbol conflict detection.
    /// </summary>
    /// <remarks>
    /// Forwarded from <see cref="ConflictDetector.ConflictDetectionResult"/> for backward compatibility.
    /// </remarks>
    public class ConflictDetectionResult : ConflictDetector.ConflictDetectionResult
    {
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
        var result = _positionResolver.GetSymbolAtPosition(sourceCode, lineNumber, columnNumber);

        // Convert to facade result type for backward compatibility
        return result.Success
            ? SymbolResolutionResult.Successful(result.Symbol!, result.Node!)
            : SymbolResolutionResult.Failed(result.ErrorMessage!);
    }

    /// <summary>
    /// Gets the symbol at a specific line and column position using an existing semantic model.
    /// This overload maintains SyntaxTree identity for operations requiring consistent compilation context.
    /// </summary>
    /// <remarks>
    /// See <see cref="PositionBasedResolver.GetSymbolAtPosition(SemanticModel, SyntaxTree, int, int)"/>
    /// for detailed documentation on SyntaxTree identity requirements.
    /// </remarks>
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
        var result = _positionResolver.GetSymbolAtPosition(semanticModel, syntaxTree, lineNumber, columnNumber);

        // Convert to facade result type for backward compatibility
        return result.Success
            ? SymbolResolutionResult.Successful(result.Symbol!, result.Node!)
            : SymbolResolutionResult.Failed(result.ErrorMessage!);
    }

    /// <summary>
    /// Detects if a symbol name would conflict with existing symbols in a given scope.
    /// </summary>
    /// <remarks>
    /// See <see cref="ConflictDetector.FindSymbolConflicts"/> for detailed documentation
    /// on conflict detection strategies and checked symbol types.
    /// </remarks>
    /// <param name="semanticModel">The semantic model for analysis.</param>
    /// <param name="symbolName">The proposed symbol name to check for conflicts.</param>
    /// <param name="scopeNode">The syntax node representing the scope to check (typically a method or class declaration).</param>
    /// <returns>A <see cref="ConflictDetectionResult"/> indicating whether conflicts exist, including the list of conflicting symbols if any.</returns>
    public ConflictDetectionResult FindSymbolConflicts(
        SemanticModel semanticModel,
        string symbolName,
        SyntaxNode scopeNode)
    {
        var result = _conflictDetector.FindSymbolConflicts(semanticModel, symbolName, scopeNode);

        // Result already has compatible type
        return new ConflictDetectionResult
        {
            HasConflicts = result.HasConflicts,
            Conflicts = result.Conflicts,
            ConflictDescription = result.ConflictDescription
        };
    }

    /// <summary>
    /// Analyzes the scope and accessibility of a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to analyze.</param>
    /// <returns>Information about the symbol's scope.</returns>
    public SymbolScopeInfo AnalyzeSymbolScope(ISymbol symbol)
    {
        return _scopeAnalyzer.AnalyzeSymbolScope(symbol);
    }

    /// <summary>
    /// Finds all references to a symbol within a syntax tree.
    /// Note: This is a simplified version that only searches within the provided compilation.
    /// For full cross-project reference finding, use SymbolFinder with a Solution-based approach.
    /// </summary>
    /// <param name="symbol">The symbol to find references for.</param>
    /// <param name="compilation">The compilation to search.</param>
    /// <returns>A list of reference locations.</returns>
    public List<Location> GetAllReferences(ISymbol symbol, Compilation compilation)
    {
        return _referenceLocator.GetAllReferences(symbol, compilation);
    }
}
