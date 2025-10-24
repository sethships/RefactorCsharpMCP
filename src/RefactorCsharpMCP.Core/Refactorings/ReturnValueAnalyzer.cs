using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Analyzes extracted code to determine appropriate return type (void, single value, or tuple).
/// </summary>
internal class ReturnValueAnalyzer
{
    private readonly ILogger? _logger;

    /// <summary>
    /// C# reserved keywords that cannot be used as variable names (80 keywords per Roslyn IsReservedKeyword).
    /// </summary>
    private static readonly HashSet<string> CSharpKeywords = new(80, StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate",
        "do", "double", "else", "enum", "event", "explicit", "extern", "false",
        "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
        "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        "__arglist", "__makeref", "__reftype", "__refvalue"
    };

    /// <summary>
    /// Initializes a new instance of the ReturnValueAnalyzer class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public ReturnValueAnalyzer(ILogger? logger = null)
    {
        _logger = logger;
    }
    /// <summary>
    /// Detects the return type needed for an extracted method based on data flow and control flow analysis.
    /// </summary>
    /// <param name="dataFlowInfo">Data flow analysis results containing output variables.</param>
    /// <param name="statements">The statements being extracted.</param>
    /// <param name="semanticModel">Semantic model for type information.</param>
    /// <param name="position">Position in source code to check for symbol conflicts.</param>
    /// <returns>Information about the required return type.</returns>
    public ReturnTypeInfo DetectReturnType(
        DataFlowInfo dataFlowInfo,
        List<StatementSyntax> statements,
        SemanticModel semanticModel,
        int position)
    {
        _logger?.LogDebug("Analyzing return type for {StatementCount} statements", statements.Count);

        // Check for explicit return statements first
        var returnStatements = GetReturnStatements(statements);
        var hasExplicitReturns = returnStatements.Any();

        // Analyze output variables from data flow
        var outputVariables = dataFlowInfo.OutputVariables ?? new List<string>();

        _logger?.LogDebug(
            "Found {ExplicitReturns} explicit returns, {OutputVars} output variables",
            returnStatements.Count,
            outputVariables.Count);

        // Decision logic
        if (!hasExplicitReturns && !outputVariables.Any())
        {
            // No returns, no outputs → void
            _logger?.LogDebug("Detected void return (no returns, no outputs)");
            return new ReturnTypeInfo
            {
                Kind = ReturnKind.Void
            };
        }

        if (hasExplicitReturns)
        {
            // Has explicit returns - analyze return expressions
            _logger?.LogDebug("Analyzing explicit return statements");
            return AnalyzeExplicitReturns(returnStatements, semanticModel, position);
        }

        // Has output variables but no explicit returns
        _logger?.LogDebug("Analyzing output variables for return type");
        return AnalyzeOutputVariables(outputVariables, dataFlowInfo.Parameters, semanticModel);
    }

    /// <summary>
    /// Gets all return statements from a list of statements (including nested ones).
    /// </summary>
    private List<ReturnStatementSyntax> GetReturnStatements(List<StatementSyntax> statements)
    {
        var returns = new List<ReturnStatementSyntax>();

        foreach (var statement in statements)
        {
            // Find all return statements including in nested blocks
            returns.AddRange(statement.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>());
        }

        return returns;
    }

    /// <summary>
    /// Analyzes explicit return statements to determine return type.
    /// </summary>
    private ReturnTypeInfo AnalyzeExplicitReturns(
        List<ReturnStatementSyntax> returnStatements,
        SemanticModel semanticModel,
        int position)
    {
        // Get return expressions (filter out void returns like "return;")
        var returnExpressions = returnStatements
            .Where(r => r.Expression != null)
            .Select(r => r.Expression!)
            .ToList();

        if (!returnExpressions.Any())
        {
            // All returns are "return;" with no value → void
            _logger?.LogDebug("All return statements are void (no expressions)");
            return new ReturnTypeInfo { Kind = ReturnKind.Void };
        }

        // Get types of all return expressions
        var returnTypes = returnExpressions
            .Select(expr => semanticModel.GetTypeInfo(expr).Type)
            .Where(t => t != null)
            .ToList();

        _logger?.LogDebug("Analyzed {TypeCount} return types", returnTypes.Count);

        if (!returnTypes.Any())
        {
            // Couldn't determine types - default to void
            _logger?.LogWarning("Could not determine return types from expressions");
            return new ReturnTypeInfo { Kind = ReturnKind.Void };
        }

        // Check if all returns are the same type
        var firstType = returnTypes.First();
        var allSameType = returnTypes.All(t => SymbolEqualityComparer.Default.Equals(t, firstType));

        if (allSameType)
        {
            // Single consistent return type
            _logger?.LogDebug("Single consistent return type: {TypeName}", firstType?.Name);
            return new ReturnTypeInfo
            {
                Kind = ReturnKind.Single,
                SingleReturnType = firstType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object",
                SingleReturnName = GenerateUniqueVariableName("result", semanticModel, position)
            };
        }

        // Multiple different return types - would need tuple or complex refactoring
        // For now, treat as single with most common/first type
        // TODO: Consider if we should support tuple returns from mixed return statements
        _logger?.LogWarning(
            "Mixed return types detected, using first type: {TypeName}",
            firstType?.Name ?? "unknown");
        return new ReturnTypeInfo
        {
            Kind = ReturnKind.Single,
            SingleReturnType = firstType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object",
            SingleReturnName = GenerateUniqueVariableName("result", semanticModel, position)
        };
    }

    /// <summary>
    /// Analyzes output variables to determine if single or multiple returns needed.
    /// </summary>
    private ReturnTypeInfo AnalyzeOutputVariables(
        List<string> outputVariables,
        List<ParameterInfo> parameters,
        SemanticModel semanticModel)
    {
        if (outputVariables.Count == 0)
        {
            return new ReturnTypeInfo { Kind = ReturnKind.Void };
        }

        if (outputVariables.Count == 1)
        {
            // Single output variable
            var varName = outputVariables[0];

            // Try to find the type from parameters (it should be in the parameter list as input)
            var parameter = parameters.FirstOrDefault(p => p.Name == varName);
            if (parameter != null)
            {
                return new ReturnTypeInfo
                {
                    Kind = ReturnKind.Single,
                    SingleReturnType = parameter.Type,
                    SingleReturnName = varName
                };
            }

            // Fallback - couldn't determine type
            return new ReturnTypeInfo
            {
                Kind = ReturnKind.Single,
                SingleReturnType = "object",
                SingleReturnName = varName
            };
        }

        // Multiple output variables → tuple return needed
        var tupleElements = new List<(string Name, string Type)>();

        foreach (var varName in outputVariables)
        {
            var parameter = parameters.FirstOrDefault(p => p.Name == varName);
            var type = parameter?.Type ?? "object";
            tupleElements.Add((varName, type));
        }

        return new ReturnTypeInfo
        {
            Kind = ReturnKind.Multiple,
            MultipleReturns = tupleElements
        };
    }

    /// <summary>
    /// Generates a unique variable name that doesn't conflict with existing variables in scope or C# keywords.
    /// </summary>
    /// <param name="baseName">The preferred base name (e.g., "result").</param>
    /// <param name="semanticModel">Semantic model for symbol lookup.</param>
    /// <param name="position">Position in source to check scope.</param>
    /// <returns>A unique variable name that won't conflict with existing symbols or keywords.</returns>
    internal string GenerateUniqueVariableName(
        string baseName,
        SemanticModel semanticModel,
        int position)
    {
        _logger?.LogDebug("Generating unique variable name from base: {BaseName}", baseName);

        // Get all symbols in scope at the given position
        var symbolsInScope = semanticModel.LookupSymbols(position);
        var existingNames = new HashSet<string>(
            symbolsInScope.Select(s => s.Name),
            StringComparer.Ordinal);

        // Add C# keywords to forbidden names
        existingNames.UnionWith(CSharpKeywords);

        // If base name doesn't conflict, use it
        if (!existingNames.Contains(baseName))
        {
            _logger?.LogDebug("Base name '{BaseName}' is unique, using it", baseName);
            return baseName;
        }

        // Generate unique name: result1, result2, etc.
        int counter = 1;
        string candidateName;
        do
        {
            candidateName = $"{baseName}{counter}";
            counter++;
        } while (existingNames.Contains(candidateName));

        _logger?.LogDebug("Generated unique name: {UniqueName} (base: {BaseName})", candidateName, baseName);
        return candidateName;
    }
}

/// <summary>
/// Represents the kind of return type needed for an extracted method.
/// </summary>
internal enum ReturnKind
{
    /// <summary>
    /// No return value (void method).
    /// </summary>
    Void,

    /// <summary>
    /// Single return value.
    /// </summary>
    Single,

    /// <summary>
    /// Multiple return values (tuple).
    /// </summary>
    Multiple
}

/// <summary>
/// Contains information about the return type for an extracted method.
/// </summary>
internal class ReturnTypeInfo
{
    /// <summary>
    /// The kind of return (void, single, or multiple).
    /// </summary>
    public ReturnKind Kind { get; set; }

    /// <summary>
    /// The type of a single return value (null if not single).
    /// </summary>
    public string? SingleReturnType { get; set; }

    /// <summary>
    /// The variable name for a single return value (null if not single).
    /// </summary>
    public string? SingleReturnName { get; set; }

    /// <summary>
    /// List of (name, type) pairs for multiple return values (empty if not multiple).
    /// </summary>
    public List<(string Name, string Type)> MultipleReturns { get; set; } = new();
}
