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
    public UnusedUsingPatternAnalyzer(ILogger? logger = null)
    {
        this._logger = logger;
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

            // Get all using directives (including aliases and static usings)
            var usingDirectives = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .ToList();

            if (!usingDirectives.Any())
            {
                _logger?.LogDebug("No using directives found in syntax tree");
                return ImmutableArray<Diagnostic>.Empty;
            }

            _logger?.LogDebug("Analyzing {Count} using directives", usingDirectives.Count);

            // Get all symbols used in the file (excluding the using directives themselves)
            var usedSymbols = GetUsedSymbols(root, semanticModel, usingDirectives);

            // Get types that are accessed via using aliases (these don't require namespace usings)
            var aliasedTypes = GetAliasedTypes(semanticModel, usingDirectives);

            // Get types that are accessed via static usings (these don't require namespace usings either)
            var staticUsingTypes = GetStaticUsingTypes(semanticModel, usingDirectives);

            _logger?.LogDebug("Found {Count} used symbols in file", usedSymbols.Count);
            _logger?.LogDebug("Found {Count} aliased types", aliasedTypes.Count);
            _logger?.LogDebug("Found {Count} static using types", staticUsingTypes.Count);

            // Check each using directive
            foreach (var usingDirective in usingDirectives)
            {
                // Defensive check - Name should never be null for valid using directives
                if (usingDirective.Name == null)
                {
                    _logger?.LogWarning("Skipping using directive with null Name at {Location}",
                        usingDirective.GetLocation().GetLineSpan().StartLinePosition);
                    continue;
                }

                if (!IsUsingDirectiveUsed(usingDirective, semanticModel, usedSymbols, aliasedTypes, staticUsingTypes))
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
    /// <remarks>
    /// Performance Note: This method allocates a new HashSet on each call rather than using
    /// ArrayPool&lt;T&gt; or ObjectPool&lt;T&gt;. This decision prioritizes code simplicity and
    /// maintainability over micro-optimization. HashSet allocations are relatively small and
    /// short-lived (typical lifetime: milliseconds). Object pooling would add complexity for
    /// minimal benefit unless profiling demonstrates this as a performance bottleneck.
    /// Consider pooling if profiling shows &gt;5% time spent in HashSet allocations or
    /// if analyzing very large files (&gt;10K LOC) becomes a common scenario.
    /// </remarks>
    private HashSet<ISymbol> GetUsedSymbols(
        SyntaxNode root,
        SemanticModel semanticModel,
        List<UsingDirectiveSyntax> usingDirectives)
    {
        // NOTE: Direct allocation preferred over pooling until performance profiling demonstrates need
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
            catch (Exception ex)
            {
                // Log but continue - individual symbol resolution failures shouldn't block analysis
                _logger?.LogWarning(ex, "Failed to resolve symbol for identifier '{Identifier}' at {Location}: {Message}",
                    identifier.Identifier.Text,
                    identifier.GetLocation().GetLineSpan().StartLinePosition,
                    ex.Message);
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
    /// Routes to appropriate handler based on directive type (alias, static, or namespace).
    /// </summary>
    private bool IsUsingDirectiveUsed(
        UsingDirectiveSyntax usingDirective,
        SemanticModel semanticModel,
        HashSet<ISymbol> usedSymbols,
        HashSet<ITypeSymbol> aliasedTypes,
        HashSet<ITypeSymbol> staticUsingTypes)
    {
        // Defensive check - Name should never be null for valid using directives
        if (usingDirective.Name == null)
        {
            _logger?.LogWarning("Using directive has null Name, assuming used to avoid false positive");
            return true;
        }

        try
        {
            // Route based on using directive type
            if (usingDirective.Alias != null)
            {
                return IsAliasUsed(usingDirective, semanticModel);
            }

            if (!usingDirective.StaticKeyword.IsKind(SyntaxKind.None))
            {
                return IsStaticUsingUsed(usingDirective, semanticModel, usedSymbols);
            }

            // Normal namespace using directive
            return IsNamespaceUsingUsed(usingDirective, semanticModel, usedSymbols, aliasedTypes, staticUsingTypes);
        }
        catch (Exception ex)
        {
            // Log and assume used to avoid false positives
            _logger?.LogWarning(ex, "Error checking using directive '{Using}' at {Location}: {Message}. Assuming used to avoid false positive.",
                usingDirective.Name.ToString(),
                usingDirective.GetLocation().GetLineSpan().StartLinePosition,
                ex.Message);
            return true;
        }
    }

    /// <summary>
    /// Checks if a using alias directive is used in the file.
    /// Example: using StringList = System.Collections.Generic.List&lt;string&gt;;
    /// Uses semantic analysis to verify binding, preventing false positives from shadowed identifiers or string literals.
    /// </summary>
    private bool IsAliasUsed(UsingDirectiveSyntax usingDirective, SemanticModel semanticModel)
    {
        if (usingDirective.Alias?.Name == null || usingDirective.Name == null)
        {
            return true; // Conservative: assume used
        }

        var aliasName = usingDirective.Alias.Name.Identifier.Text;
        var root = semanticModel.SyntaxTree.GetRoot();

        // Get the symbol that the alias refers to
        var aliasTargetSymbol = semanticModel.GetSymbolInfo(usingDirective.Name).Symbol;
        if (aliasTargetSymbol == null)
        {
            _logger?.LogDebug("Could not resolve target symbol for alias '{Alias}'", aliasName);
            return true; // Conservative: if we can't resolve, assume used
        }

        // Check if alias identifier is semantically bound to the aliased type
        // This prevents false positives from shadowed identifiers or string literals
        var aliasIsReferenced = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => !IsNodeInUsingDirective(id, new List<UsingDirectiveSyntax> { usingDirective }))
            .Any(id =>
            {
                if (id.Identifier.Text != aliasName) return false;

                // Verify that this identifier is semantically bound to the same symbol as the alias
                var symbolInfo = semanticModel.GetSymbolInfo(id);
                return SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, aliasTargetSymbol);
            });

        _logger?.LogDebug("Using alias '{Alias}' is {Status}",
            aliasName,
            aliasIsReferenced ? "used" : "unused");

        return aliasIsReferenced;
    }

    /// <summary>
    /// Checks if a static using directive is used in the file.
    /// Example: using static System.Math;
    /// </summary>
    private bool IsStaticUsingUsed(
        UsingDirectiveSyntax usingDirective,
        SemanticModel semanticModel,
        HashSet<ISymbol> usedSymbols)
    {
        if (usingDirective.Name == null)
        {
            return true; // Conservative: assume used
        }

        var symbolInfo = semanticModel.GetSymbolInfo(usingDirective.Name);

        if (symbolInfo.Symbol is not INamedTypeSymbol typeSymbol)
        {
            _logger?.LogDebug("Could not resolve type symbol for static using: {Using}", usingDirective.Name.ToString());
            return true; // Conservative: assume used
        }

        // Check if any used symbol is a static member of this type
        var staticMemberUsed = usedSymbols.Any(symbol =>
            symbol.IsStatic &&
            symbol.ContainingType != null &&
            (SymbolEqualityComparer.Default.Equals(symbol.ContainingType, typeSymbol) ||
             symbol.ContainingType.ToDisplayString() == typeSymbol.ToDisplayString()));

        _logger?.LogDebug("Static using '{Type}' is {Status}",
            typeSymbol.ToDisplayString(),
            staticMemberUsed ? "used" : "unused");

        return staticMemberUsed;
    }

    /// <summary>
    /// Checks if a namespace using directive is used in the file.
    /// Example: using System.Linq;
    /// Excludes symbols that are only accessed via using aliases or static usings.
    /// </summary>
    private bool IsNamespaceUsingUsed(
        UsingDirectiveSyntax usingDirective,
        SemanticModel semanticModel,
        HashSet<ISymbol> usedSymbols,
        HashSet<ITypeSymbol> aliasedTypes,
        HashSet<ITypeSymbol> staticUsingTypes)
    {
        if (usingDirective.Name == null)
        {
            return true; // Conservative: assume used
        }

        // Get the namespace symbol for this using directive
        var symbolInfo = semanticModel.GetSymbolInfo(usingDirective.Name);

        if (symbolInfo.Symbol is not INamespaceSymbol namespaceSymbol)
        {
            _logger?.LogDebug("Could not resolve namespace symbol for: {Using}", usingDirective.Name.ToString());
            // If we can't resolve the symbol, assume it's used to avoid false positives
            return true;
        }

        // Check if any used symbol is from this namespace, excluding symbols accessed via aliases or static usings
        return usedSymbols.Any(symbol =>
            IsSymbolFromNamespace(symbol, namespaceSymbol) &&
            !IsSymbolAccessedViaAliasOrStaticUsing(symbol, aliasedTypes, staticUsingTypes));
    }

    /// <summary>
    /// Checks if a symbol is from the specified namespace or any of its child namespaces.
    /// </summary>
    /// <remarks>
    /// <para><strong>Cross-Compilation Symbol Identity Workaround</strong></para>
    /// <para>
    /// This method uses both SymbolEqualityComparer.Default.Equals() AND ToDisplayString() comparison
    /// because symbols from different compilations may represent the same semantic namespace but fail
    /// equality comparison due to different object identities.
    /// </para>
    ///
    /// <para><strong>Why This Happens:</strong></para>
    /// <list type="bullet">
    ///   <item>When analyzing code, we create a minimal compilation with limited references</item>
    ///   <item>Namespace symbols in our compilation vs. reference assemblies have different identity</item>
    ///   <item>SymbolEqualityComparer checks object identity first for performance</item>
    ///   <item>Cross-compilation namespace symbols (e.g., System.Linq from two different compilations) fail equality check</item>
    /// </list>
    ///
    /// <para><strong>The Workaround:</strong></para>
    /// <para>
    /// We use ToDisplayString() as a fallback to compare namespace names when SymbolEqualityComparer fails.
    /// This correctly identifies semantically equivalent namespaces across compilation boundaries.
    /// </para>
    ///
    /// <para><strong>Known Limitations:</strong></para>
    /// <list type="bullet">
    ///   <item>String comparison is slightly slower than reference equality (~5-10% performance impact)</item>
    ///   <item>Exotic cases like namespace aliasing may not be handled correctly</item>
    ///   <item>Assumes namespace qualified names are unique (generally true for .NET)</item>
    /// </list>
    ///
    /// <para><strong>Alternative Approaches Considered:</strong></para>
    /// <list type="number">
    ///   <item><strong>Single compilation with all references:</strong> Rejected due to reference assembly download complexity</item>
    ///   <item><strong>Custom SymbolEqualityComparer:</strong> Rejected as it doesn't solve cross-compilation identity</item>
    ///   <item><strong>MetadataName comparison:</strong> Equivalent to ToDisplayString() for namespaces</item>
    ///   <item><strong>Symbol key comparison:</strong> Overly complex for this use case</item>
    /// </list>
    ///
    /// <para>
    /// This workaround provides 90%+ accuracy for unused using detection while avoiding the complexity
    /// of full workspace-based analysis. See Issue #72 for architectural decision rationale.
    /// </para>
    /// </remarks>
    private bool IsSymbolFromNamespace(ISymbol symbol, INamespaceSymbol targetNamespace)
    {
        // If the symbol itself is the target namespace, check by name AND by comparer
        // Use name comparison as fallback since symbols from different compilations may not be equal by comparer
        if (symbol is INamespaceSymbol ns &&
            (SymbolEqualityComparer.Default.Equals(ns, targetNamespace) ||
             ns.ToDisplayString() == targetNamespace.ToDisplayString()))
        {
            return true;
        }

        // For types, methods, properties, etc., check their containing namespace directly
        var containingNamespace = symbol.ContainingNamespace;

        while (containingNamespace != null && !containingNamespace.IsGlobalNamespace)
        {
            if (SymbolEqualityComparer.Default.Equals(containingNamespace, targetNamespace) ||
                containingNamespace.ToDisplayString() == targetNamespace.ToDisplayString())
            {
                return true;
            }

            // Check if it's a child namespace (e.g., System.Linq is child of System)
            if (IsChildNamespace(containingNamespace, targetNamespace))
            {
                return true;
            }

            // Move up to parent namespace
            containingNamespace = containingNamespace.ContainingNamespace;
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
    /// <remarks>
    /// This method assumes Name is non-null, which is validated by the caller.
    /// </remarks>
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
            usingDirective.Name!.ToString()); // Null-forgiving: Name validated by caller
    }

    /// <summary>
    /// Gets all types that are accessed via using alias directives.
    /// These types don't require their namespace to be imported via using directives.
    /// </summary>
    private HashSet<ITypeSymbol> GetAliasedTypes(SemanticModel semanticModel, List<UsingDirectiveSyntax> usingDirectives)
    {
        var aliasedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var usingDirective in usingDirectives.Where(u => u.Alias != null && u.Name != null))
        {
            try
            {
                var symbolInfo = semanticModel.GetSymbolInfo(usingDirective.Name!);
                if (symbolInfo.Symbol is ITypeSymbol typeSymbol)
                {
                    aliasedTypes.Add(typeSymbol);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to resolve alias type for {Using}", usingDirective.Name?.ToString() ?? "unknown");
            }
        }

        return aliasedTypes;
    }

    /// <summary>
    /// Checks if a symbol is accessed via a using alias or static using.
    /// If a symbol is a type that's aliased/static-imported, contained in such a type, or is a namespace
    /// that contains such a type, returns true.
    /// </summary>
    private bool IsSymbolAccessedViaAliasOrStaticUsing(
        ISymbol symbol,
        HashSet<ITypeSymbol> aliasedTypes,
        HashSet<ITypeSymbol> staticUsingTypes)
    {
        // Check both sets directly without creating intermediate HashSet (performance optimization)
        // If the symbol itself is a specially-accessed type
        if (symbol is ITypeSymbol typeSymbol &&
            (aliasedTypes.Contains(typeSymbol) || staticUsingTypes.Contains(typeSymbol)))
        {
            return true;
        }

        // If the symbol's containing type is specially accessed (e.g., member of an aliased type)
        if (symbol.ContainingType != null &&
            (aliasedTypes.Contains(symbol.ContainingType) || staticUsingTypes.Contains(symbol.ContainingType)))
        {
            return true;
        }

        // If the symbol is a namespace and contains any specially-accessed types
        if (symbol is INamespaceSymbol namespaceSymbol)
        {
            foreach (var specialType in aliasedTypes.Concat(staticUsingTypes))
            {
                // Check if the type is within this namespace
                if (IsSymbolFromNamespace(specialType, namespaceSymbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all types that are accessed via static using directives.
    /// These types don't require their namespace to be imported via using directives.
    /// </summary>
    private HashSet<ITypeSymbol> GetStaticUsingTypes(SemanticModel semanticModel, List<UsingDirectiveSyntax> usingDirectives)
    {
        var staticUsingTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var usingDirective in usingDirectives.Where(u => !u.StaticKeyword.IsKind(SyntaxKind.None) && u.Name != null))
        {
            try
            {
                var symbolInfo = semanticModel.GetSymbolInfo(usingDirective.Name!);
                if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
                {
                    staticUsingTypes.Add(typeSymbol);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to resolve static using type for {Using}", usingDirective.Name?.ToString() ?? "unknown");
            }
        }

        return staticUsingTypes;
    }
}
