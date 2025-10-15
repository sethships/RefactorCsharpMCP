using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace RefactorCsharpMCP.Core.Utilities;

/// <summary>
/// Provides utilities for resolving and analyzing symbols in C# code.
/// Used by refactorings that need to locate symbols by position, detect conflicts, and analyze scope.
/// </summary>
public class SymbolResolutionHelper
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
    /// Result of symbol conflict detection.
    /// </summary>
    public class ConflictDetectionResult
    {
        /// <summary>
        /// Indicates whether conflicts were found.
        /// </summary>
        public bool HasConflicts { get; init; }

        /// <summary>
        /// List of conflicting symbols.
        /// </summary>
        public List<ISymbol> Conflicts { get; init; } = new();

        /// <summary>
        /// Human-readable description of conflicts.
        /// </summary>
        public string? ConflictDescription { get; init; }
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
    /// Detects if a symbol name would conflict with existing symbols in a given scope.
    /// </summary>
    /// <param name="semanticModel">The semantic model for analysis.</param>
    /// <param name="symbolName">The proposed symbol name.</param>
    /// <param name="scopeNode">The syntax node representing the scope to check.</param>
    /// <returns>A result indicating whether conflicts exist.</returns>
    public ConflictDetectionResult FindSymbolConflicts(
        SemanticModel semanticModel,
        string symbolName,
        SyntaxNode scopeNode)
    {
        if (string.IsNullOrWhiteSpace(symbolName))
        {
            return new ConflictDetectionResult
            {
                HasConflicts = false,
                ConflictDescription = "Symbol name is empty."
            };
        }

        try
        {
            // Use HashSet to automatically handle uniqueness and avoid duplicates
            var conflicts = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            // Check for local variables with the same name
            var localSymbols = semanticModel.LookupSymbols(scopeNode.SpanStart, name: symbolName);
            foreach (var symbol in localSymbols.Where(s => s.Kind == SymbolKind.Local || s.Kind == SymbolKind.Parameter))
            {
                conflicts.Add(symbol);
            }

            // Check for methods with the same name
            if (scopeNode is ClassDeclarationSyntax classDeclaration)
            {
                var methodSymbols = classDeclaration.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Identifier.Text == symbolName)
                    .Select(m => semanticModel.GetDeclaredSymbol(m))
                    .Where(s => s != null)
                    .Cast<ISymbol>();

                foreach (var symbol in methodSymbols)
                {
                    conflicts.Add(symbol);
                }
            }

            // Check for fields with the same name
            var fieldSymbols = semanticModel.LookupSymbols(scopeNode.SpanStart, name: symbolName)
                .Where(s => s.Kind == SymbolKind.Field || s.Kind == SymbolKind.Property);

            foreach (var symbol in fieldSymbols)
            {
                conflicts.Add(symbol);
            }

            if (conflicts.Any())
            {
                var conflictTypes = string.Join(", ", conflicts.Select(s => $"{s.Kind} '{s.Name}'").Distinct());
                return new ConflictDetectionResult
                {
                    HasConflicts = true,
                    Conflicts = conflicts.ToList(),
                    ConflictDescription = $"Name '{symbolName}' conflicts with existing symbols: {conflictTypes}"
                };
            }

            return new ConflictDetectionResult
            {
                HasConflicts = false,
                ConflictDescription = null
            };
        }
        catch (Exception ex)
        {
            // Sanitize exception message to avoid leaking internal details
            var errorCategory = ex switch
            {
                ArgumentException => "InvalidArgument",
                InvalidOperationException => "InvalidState",
                _ => "InternalError"
            };
            return new ConflictDetectionResult
            {
                HasConflicts = false,
                ConflictDescription = $"Error detecting conflicts ({errorCategory}). Unable to determine if conflicts exist."
            };
        }
    }

    /// <summary>
    /// Analyzes the scope and accessibility of a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to analyze.</param>
    /// <returns>Information about the symbol's scope.</returns>
    public SymbolScopeInfo AnalyzeSymbolScope(ISymbol symbol)
    {
        if (symbol == null)
        {
            return new SymbolScopeInfo
            {
                ScopeName = "Unknown",
                IsLocal = false,
                IsParameter = false,
                IsField = false,
                IsMethod = false,
                IsPublic = false
            };
        }

        var scopeName = symbol.ContainingType?.Name ?? symbol.ContainingNamespace?.Name ?? "Global";

        return new SymbolScopeInfo
        {
            ScopeName = scopeName,
            IsLocal = symbol.Kind == SymbolKind.Local,
            IsParameter = symbol.Kind == SymbolKind.Parameter,
            IsField = symbol.Kind == SymbolKind.Field,
            IsMethod = symbol.Kind == SymbolKind.Method,
            IsProperty = symbol.Kind == SymbolKind.Property,
            IsPublic = symbol.DeclaredAccessibility == Accessibility.Public,
            IsPrivate = symbol.DeclaredAccessibility == Accessibility.Private,
            IsProtected = symbol.DeclaredAccessibility == Accessibility.Protected,
            IsInternal = symbol.DeclaredAccessibility == Accessibility.Internal
        };
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
        if (symbol == null || compilation == null)
        {
            return new List<Location>();
        }

        try
        {
            var references = new List<Location>();

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                // Find all identifier nodes that reference this symbol
                var identifiers = root.DescendantNodes()
                    .OfType<IdentifierNameSyntax>();

                foreach (var identifier in identifiers)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(identifier);
                    if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, symbol))
                    {
                        references.Add(identifier.GetLocation());
                    }
                }
            }

            return references;
        }
        catch (Exception ex)
        {
            // Log the exception for debugging purposes
            // Debug.WriteLine is compiled out in Release builds
            System.Diagnostics.Debug.WriteLine($"Error finding references for symbol '{symbol?.Name}': {ex.GetType().Name} - {ex.Message}");

            // Return empty list as fallback
            // Note: This risks false negatives in SafeDelete (deleting methods that are actually referenced)
            // Consider alternative: throw exception or return Result<List<Location>, string>
            return new List<Location>();
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

/// <summary>
/// Information about a symbol's scope and accessibility.
/// </summary>
public class SymbolScopeInfo
{
    public required string ScopeName { get; init; }
    public required bool IsLocal { get; init; }
    public required bool IsParameter { get; init; }
    public required bool IsField { get; init; }
    public required bool IsMethod { get; init; }
    public bool IsProperty { get; init; }
    public bool IsPublic { get; init; }
    public bool IsPrivate { get; init; }
    public bool IsProtected { get; init; }
    public bool IsInternal { get; init; }
}
