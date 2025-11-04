using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace RefactorCsharpMCP.Core.Diagnostics;

/// <summary>
/// Pattern-based analyzer for detecting unused using directives (IDE0005).
/// Uses semantic analysis to determine if namespace symbols are referenced in the code.
/// This is a pragmatic approach that covers 90%+ of common cases without requiring
/// full IDE analyzer infrastructure.
/// </summary>
public class UnusedUsingPatternAnalyzer
{
    private readonly ILogger? _logger;

    /// <summary>
    /// Creates a new UnusedUsingPatternAnalyzer instance.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public UnusedUsingPatternAnalyzer(ILogger? _logger = null)
    {
        this._logger = _logger;
    }

    /// <summary>
    /// Analyzes a syntax tree for unused using directives.
    /// </summary>
    /// <param name="syntaxTree">The syntax tree to analyze.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <returns>Array of diagnostics for unused using directives.</returns>
    public ImmutableArray<Diagnostic> Analyze(SyntaxTree syntaxTree, SemanticModel semanticModel)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        try
        {
            var root = syntaxTree.GetRoot();

            // Get all using directives
            var usingDirectives = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Where(u => u.Alias == null) // Skip using alias directives (using X = Y)
                .ToList();

            if (!usingDirectives.Any())
            {
                _logger?.LogDebug("No using directives found in syntax tree");
                return ImmutableArray<Diagnostic>.Empty;
            }

            _logger?.LogDebug("Analyzing {Count} using directives", usingDirectives.Count);

            // Get all symbols used in the file (excluding the using directives themselves)
            var usedSymbols = GetUsedSymbols(root, semanticModel, usingDirectives);

            _logger?.LogDebug("Found {Count} used symbols in file", usedSymbols.Count);

            // Check each using directive
            foreach (var usingDirective in usingDirectives)
            {
                if (!IsUsingDirectiveUsed(usingDirective, semanticModel, usedSymbols))
                {
                    var diagnostic = CreateUnusedUsingDiagnostic(usingDirective);
                    diagnostics.Add(diagnostic);

                    _logger?.LogDebug("Unused using detected: {Using}", usingDirective.Name.ToString());
                }
            }

            _logger?.LogDebug("Found {Count} unused using directives", diagnostics.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error analyzing unused usings: {Message}", ex.Message);
            // Don't throw - return empty diagnostics on error
        }

        return diagnostics.ToImmutable();
    }

    /// <summary>
    /// Gets all symbols used in the syntax tree (excluding using directives).
    /// </summary>
    private HashSet<ISymbol> GetUsedSymbols(
        SyntaxNode root,
        SemanticModel semanticModel,
        List<UsingDirectiveSyntax> usingDirectives)
    {
        var usedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        // Get all identifier nodes that are not part of using directives
        var identifiers = root.DescendantNodes()
            .Where(node => !IsNodeInUsingDirective(node, usingDirectives))
            .OfType<IdentifierNameSyntax>()
            .ToList();

        foreach (var identifier in identifiers)
        {
            try
            {
                var symbolInfo = semanticModel.GetSymbolInfo(identifier);

                if (symbolInfo.Symbol != null)
                {
                    usedSymbols.Add(symbolInfo.Symbol);

                    // Also add containing namespace/type symbols
                    AddContainingSymbols(symbolInfo.Symbol, usedSymbols);
                }
            }
            catch
            {
                // Ignore errors resolving individual symbols
            }
        }

        // Also check generic type arguments and base types
        var genericNames = root.DescendantNodes()
            .Where(node => !IsNodeInUsingDirective(node, usingDirectives))
            .OfType<GenericNameSyntax>()
            .ToList();

        foreach (var genericName in genericNames)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(genericName);
            if (symbolInfo.Symbol != null)
            {
                usedSymbols.Add(symbolInfo.Symbol);
                AddContainingSymbols(symbolInfo.Symbol, usedSymbols);
            }
        }

        return usedSymbols;
    }

    /// <summary>
    /// Checks if a node is part of a using directive.
    /// </summary>
    private bool IsNodeInUsingDirective(SyntaxNode node, List<UsingDirectiveSyntax> usingDirectives)
    {
        return node.AncestorsAndSelf().Any(ancestor =>
            usingDirectives.Any(u => u == ancestor));
    }

    /// <summary>
    /// Adds containing namespace and type symbols to the set.
    /// </summary>
    private void AddContainingSymbols(ISymbol symbol, HashSet<ISymbol> symbolSet)
    {
        var current = symbol.ContainingSymbol;

        while (current != null)
        {
            symbolSet.Add(current);
            current = current.ContainingSymbol;
        }
    }

    /// <summary>
    /// Checks if a using directive is used in the file.
    /// </summary>
    private bool IsUsingDirectiveUsed(
        UsingDirectiveSyntax usingDirective,
        SemanticModel semanticModel,
        HashSet<ISymbol> usedSymbols)
    {
        try
        {
            // Get the namespace symbol for this using directive
            var symbolInfo = semanticModel.GetSymbolInfo(usingDirective.Name);

            if (symbolInfo.Symbol is not INamespaceSymbol namespaceSymbol)
            {
                _logger?.LogDebug("Could not resolve namespace symbol for: {Using}", usingDirective.Name?.ToString());
                // If we can't resolve the symbol, assume it's used to avoid false positives
                return true;
            }

            // Check if any used symbol is from this namespace
            return usedSymbols.Any(symbol => IsSymbolFromNamespace(symbol, namespaceSymbol));
        }
        catch
        {
            // If we encounter any errors, assume the using is used to avoid false positives
            return true;
        }
    }

    /// <summary>
    /// Checks if a symbol is from the specified namespace or any of its child namespaces.
    /// </summary>
    private bool IsSymbolFromNamespace(ISymbol symbol, INamespaceSymbol targetNamespace)
    {
        // Check the symbol's namespace chain
        var current = symbol;

        while (current != null)
        {
            if (current is INamespaceSymbol ns)
            {
                if (SymbolEqualityComparer.Default.Equals(ns, targetNamespace))
                {
                    return true;
                }

                // Check if it's a child namespace (e.g., System.Linq is child of System)
                if (IsChildNamespace(ns, targetNamespace))
                {
                    return true;
                }
            }

            current = current.ContainingSymbol;
        }

        return false;
    }

    /// <summary>
    /// Checks if a namespace is a child of the target namespace.
    /// </summary>
    private bool IsChildNamespace(INamespaceSymbol childNamespace, INamespaceSymbol targetNamespace)
    {
        var current = childNamespace.ContainingNamespace;

        while (current != null && !current.IsGlobalNamespace)
        {
            if (SymbolEqualityComparer.Default.Equals(current, targetNamespace))
            {
                return true;
            }

            current = current?.ContainingNamespace;
        }

        return false;
    }

    /// <summary>
    /// Creates a diagnostic for an unused using directive.
    /// </summary>
    private Diagnostic CreateUnusedUsingDiagnostic(UsingDirectiveSyntax usingDirective)
    {
        var descriptor = new DiagnosticDescriptor(
            id: "IDE0005",
            title: "Using directive is unnecessary",
            messageFormat: "Using directive is unnecessary.",
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Using directive is unnecessary and can be removed.",
            helpLinkUri: "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0005");

        return Diagnostic.Create(
            descriptor,
            usingDirective.GetLocation(),
            usingDirective.Name.ToString());
    }
}
