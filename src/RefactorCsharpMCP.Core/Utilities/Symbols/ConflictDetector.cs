using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Utilities.Symbols;

/// <summary>
/// Detects naming conflicts between proposed symbols and existing symbols in scope.
/// Uses a combination of semantic analysis and explicit AST traversal for comprehensive detection.
/// </summary>
/// <remarks>
/// This class was extracted from SymbolResolutionHelper.cs as part of Sprint 3 decomposition (Issue #90).
/// It focuses on conflict detection optimization using HashSet and dual-strategy scanning.
/// </remarks>
public class ConflictDetector
{
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
    /// Detects if a symbol name would conflict with existing symbols in a given scope.
    /// Uses a combination of semantic analysis (LookupSymbols) and explicit AST traversal
    /// to detect all potential naming conflicts including local variables, parameters,
    /// lambda parameters, local functions, foreach variables, catch variables, methods, fields, and properties.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses two complementary approaches to ensure comprehensive conflict detection:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// <strong>Explicit AST traversal</strong>: Walks the syntax tree to find ALL local declarations
    /// (local variables, parameters, lambda parameters, local functions, foreach variables, catch variables)
    /// including those declared later in the scope that LookupSymbols cannot see.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Semantic lookup via LookupSymbols</strong>: Identifies symbols from enclosing scopes
    /// (fields, properties, methods, type members) that aren't local declarations.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// <strong>Checked symbol types:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>Local variables (including nested scopes)</description></item>
    /// <item><description>Method parameters</description></item>
    /// <item><description>Lambda parameters (simple, parenthesized, and anonymous method expressions)</description></item>
    /// <item><description>Local functions</description></item>
    /// <item><description>Foreach variables</description></item>
    /// <item><description>Catch clause variables</description></item>
    /// <item><description>Methods (when scope is a class)</description></item>
    /// <item><description>Fields and properties</description></item>
    /// </list>
    /// <para>
    /// The method is optimized to avoid redundant symbol lookups by using explicit traversal
    /// for local declarations and LookupSymbols only for enclosing scope members.
    /// </para>
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

            // Use LookupSymbols to catch symbols from enclosing scopes (fields, properties, type members)
            // Note: Local variables, parameters, and lambda parameters are checked explicitly below
            // because LookupSymbols at scope start cannot see declarations that appear later in the method
            var enclosingScopeSymbols = semanticModel.LookupSymbols(scopeNode.SpanStart, name: symbolName);
            foreach (var symbol in enclosingScopeSymbols.Where(s =>
                s.Kind != SymbolKind.Local &&    // Checked explicitly below
                s.Kind != SymbolKind.Parameter)) // Checked explicitly below
            {
                conflicts.Add(symbol);
            }

            // Explicitly check ALL local variables, parameters, and lambda parameters
            // (optimization: avoids redundant LookupSymbols calls for local declarations)
            var methodDeclaration = scopeNode.AncestorsAndSelf().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();

            // Single traversal for all local variable declarations, local functions, and foreach variables (performance optimization)
            foreach (var node in scopeNode.DescendantNodes())
            {
                // Check for local variables
                if (node is VariableDeclaratorSyntax varDeclarator &&
                    varDeclarator.Identifier.Text == symbolName)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(varDeclarator);
                    if (symbol != null)
                        conflicts.Add(symbol);
                }
                // Check for local functions
                else if (node is LocalFunctionStatementSyntax localFunction &&
                         localFunction.Identifier.Text == symbolName)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(localFunction);
                    if (symbol != null)
                        conflicts.Add(symbol);
                }
                // Check for foreach variables
                else if (node is ForEachStatementSyntax foreachStatement &&
                         foreachStatement.Identifier.Text == symbolName)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(foreachStatement);
                    if (symbol != null)
                        conflicts.Add(symbol);
                }
                // Check for catch clause variables
                else if (node is CatchClauseSyntax catchClause &&
                         catchClause.Declaration?.Identifier.Text == symbolName)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(catchClause.Declaration);
                    if (symbol != null)
                        conflicts.Add(symbol);
                }
            }

            // Check method parameters separately (parameters are not in DescendantNodes)
            if (methodDeclaration?.ParameterList != null)
            {
                foreach (var parameter in methodDeclaration.ParameterList.Parameters)
                {
                    if (parameter.Identifier.Text == symbolName)
                    {
                        var symbol = semanticModel.GetDeclaredSymbol(parameter);
                        if (symbol != null)
                            conflicts.Add(symbol);
                    }
                }
            }

            // Check lambda parameters (SimpleLambdaExpression, ParenthesizedLambdaExpression, AnonymousMethodExpression)
            // These are checked explicitly to ensure comprehensive coverage and enable LookupSymbols optimization
            var lambdaExpressions = scopeNode.DescendantNodesAndSelf().Where(n =>
                n is SimpleLambdaExpressionSyntax ||
                n is ParenthesizedLambdaExpressionSyntax ||
                n is AnonymousMethodExpressionSyntax);

            foreach (var lambda in lambdaExpressions)
            {
                if (lambda is SimpleLambdaExpressionSyntax simpleLambda &&
                    simpleLambda.Parameter.Identifier.Text == symbolName)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(simpleLambda.Parameter);
                    if (symbol != null)
                        conflicts.Add(symbol);
                }
                else if (lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda)
                {
                    foreach (var parameter in parenthesizedLambda.ParameterList.Parameters)
                    {
                        if (parameter.Identifier.Text == symbolName)
                        {
                            var symbol = semanticModel.GetDeclaredSymbol(parameter);
                            if (symbol != null)
                                conflicts.Add(symbol);
                        }
                    }
                }
                else if (lambda is AnonymousMethodExpressionSyntax anonymousMethod &&
                         anonymousMethod.ParameterList != null)
                {
                    foreach (var parameter in anonymousMethod.ParameterList.Parameters)
                    {
                        if (parameter.Identifier.Text == symbolName)
                        {
                            var symbol = semanticModel.GetDeclaredSymbol(parameter);
                            if (symbol != null)
                                conflicts.Add(symbol);
                        }
                    }
                }
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
}
