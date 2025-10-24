using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Analyzes extracted code to determine appropriate return type (void, single value, or tuple).
/// </summary>
internal class ReturnValueAnalyzer
{
    /// <summary>
    /// Detects the return type needed for an extracted method based on data flow and control flow analysis.
    /// </summary>
    /// <param name="dataFlowInfo">Data flow analysis results containing output variables.</param>
    /// <param name="statements">The statements being extracted.</param>
    /// <param name="semanticModel">Semantic model for type information.</param>
    /// <returns>Information about the required return type.</returns>
    public ReturnTypeInfo DetectReturnType(
        DataFlowInfo dataFlowInfo,
        List<StatementSyntax> statements,
        SemanticModel semanticModel)
    {
        // Check for explicit return statements first
        var returnStatements = GetReturnStatements(statements);
        var hasExplicitReturns = returnStatements.Any();

        // Analyze output variables from data flow
        var outputVariables = dataFlowInfo.OutputVariables ?? new List<string>();

        // Decision logic
        if (!hasExplicitReturns && !outputVariables.Any())
        {
            // No returns, no outputs → void
            return new ReturnTypeInfo
            {
                Kind = ReturnKind.Void
            };
        }

        if (hasExplicitReturns)
        {
            // Has explicit returns - analyze return expressions
            return AnalyzeExplicitReturns(returnStatements, semanticModel);
        }

        // Has output variables but no explicit returns
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
        SemanticModel semanticModel)
    {
        // Get return expressions (filter out void returns like "return;")
        var returnExpressions = returnStatements
            .Where(r => r.Expression != null)
            .Select(r => r.Expression!)
            .ToList();

        if (!returnExpressions.Any())
        {
            // All returns are "return;" with no value → void
            return new ReturnTypeInfo { Kind = ReturnKind.Void };
        }

        // Get types of all return expressions
        var returnTypes = returnExpressions
            .Select(expr => semanticModel.GetTypeInfo(expr).Type)
            .Where(t => t != null)
            .ToList();

        if (!returnTypes.Any())
        {
            // Couldn't determine types - default to void
            return new ReturnTypeInfo { Kind = ReturnKind.Void };
        }

        // Check if all returns are the same type
        var firstType = returnTypes.First();
        var allSameType = returnTypes.All(t => SymbolEqualityComparer.Default.Equals(t, firstType));

        if (allSameType)
        {
            // Single consistent return type
            return new ReturnTypeInfo
            {
                Kind = ReturnKind.Single,
                SingleReturnType = firstType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                SingleReturnName = "result" // Default name
            };
        }

        // Multiple different return types - would need tuple or complex refactoring
        // For now, treat as single with most common/first type
        // TODO: Consider if we should support tuple returns from mixed return statements
        return new ReturnTypeInfo
        {
            Kind = ReturnKind.Single,
            SingleReturnType = firstType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            SingleReturnName = "result"
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
