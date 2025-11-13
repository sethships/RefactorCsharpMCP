using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Analyzes data flow to determine required parameters and return values for extracted methods.
/// Uses Roslyn's DataFlowAnalysis to identify variables flowing in/out of the selected code region.
/// </summary>
internal class ParameterExtractor
{
    private readonly ReturnValueAnalyzer _returnValueAnalyzer;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of ParameterExtractor with required dependencies.
    /// </summary>
    /// <param name="returnValueAnalyzer">Analyzer for determining return value types</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public ParameterExtractor(ReturnValueAnalyzer returnValueAnalyzer, ILogger? logger = null)
    {
        _returnValueAnalyzer = returnValueAnalyzer ?? throw new ArgumentNullException(nameof(returnValueAnalyzer));
        _logger = logger;
    }

    /// <summary>
    /// Analyzes data flow for the selected statements to determine parameters and return values.
    /// </summary>
    /// <param name="semanticModel">The semantic model for symbol resolution</param>
    /// <param name="statements">The statements to be extracted</param>
    /// <param name="containingMethod">The containing method declaration</param>
    /// <returns>DataFlowInfo containing parameters and return value information</returns>
    public DataFlowInfo AnalyzeDataFlow(
        SemanticModel semanticModel,
        List<StatementSyntax> statements,
        MethodDeclarationSyntax containingMethod)
    {
        var dataFlow = new DataFlowInfo();

        if (!statements.Any()) return dataFlow;

        try
        {
            var firstStatement = statements.First();
            var lastStatement = statements.Last();

            var analysis = semanticModel.AnalyzeDataFlow(firstStatement, lastStatement);

            if (analysis == null || !analysis.Succeeded)
            {
                return dataFlow;
            }

            // Variables that flow into the selection (need to be parameters)
            // Exclude instance members (fields, properties) - they're accessible from the new method
            // Exclude 'this' parameter - instance methods have access to instance members
            dataFlow.Parameters = analysis.DataFlowsIn
                .Where(symbol => !analysis.VariablesDeclared.Contains(symbol))
                .Where(symbol => symbol is ILocalSymbol or IParameterSymbol) // Only locals and parameters
                .Where(symbol => symbol is not IParameterSymbol param || !param.IsThis) // Exclude 'this'
                .Select(symbol => new ParameterInfo
                {
                    Name = symbol.Name,
                    Type = SymbolTypeFormatter.GetSymbolType(symbol)
                })
                .ToList();

            // Variables that flow out (might need return value or out parameter)
            // Include variables that are assigned within the region but declared outside
            var outputSymbols = analysis.DataFlowsOut
                .Where(symbol => symbol is ILocalSymbol) // Only local variables can flow out
                .Cast<ILocalSymbol>()
                .ToList();

            dataFlow.OutputVariableSymbols = outputSymbols;
            dataFlow.OutputVariables = outputSymbols.Select(s => s.Name).ToList();

            // Verify that symbol and variable lists stay synchronized (CR Issue #2)
            Debug.Assert(
                dataFlow.OutputVariableSymbols.Count == dataFlow.OutputVariables.Count,
                "OutputVariableSymbols and OutputVariables must have the same count");

            // Variables declared outside but assigned inside need to be captured
            // Exclude variables already in the parameter list to avoid duplicates
            dataFlow.AssignedOutsideVariables = analysis.WrittenInside
                .Where(symbol => !analysis.VariablesDeclared.Contains(symbol))
                .Where(symbol => !analysis.DataFlowsIn.Contains(symbol)) // Exclude parameters
                .Where(symbol => symbol is ILocalSymbol)
                .Select(symbol => new ParameterInfo
                {
                    Name = symbol.Name,
                    Type = SymbolTypeFormatter.GetSymbolType(symbol)
                })
                .ToList();
        }
        catch (Exception ex)
        {
            // Data flow analysis failed - return error instead of silent degradation (CR Issue #7)
            _logger?.LogError(ex, "Data flow analysis failed");
            dataFlow.ReturnInfo = new ReturnTypeInfo
            {
                Kind = ReturnKind.Error,
                ErrorMessage = "Failed to analyze data flow for the selected code."
            };
            return dataFlow;
        }

        // Detect return type based on data flow and control flow
        var position = statements.First().SpanStart;
        dataFlow.ReturnInfo = _returnValueAnalyzer.DetectReturnType(dataFlow, statements, semanticModel, position);

        return dataFlow;
    }
}

/// <summary>
/// Contains data flow analysis results for code extraction.
/// </summary>
internal class DataFlowInfo
{
    public List<ParameterInfo> Parameters { get; set; } = new();
    public List<ILocalSymbol> OutputVariableSymbols { get; set; } = new();
    public List<string> OutputVariables { get; set; } = new();
    public List<ParameterInfo> AssignedOutsideVariables { get; set; } = new();
    public ReturnTypeInfo? ReturnInfo { get; set; }
}

/// <summary>
/// Represents a parameter with its name and type.
/// </summary>
internal class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "object";
}
