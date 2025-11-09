using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Analyzes data flow to determine required parameters and return values for extracted methods.
/// Uses Roslyn's DataFlowAnalysis to identify variables flowing in/out of the selected code region.
/// </summary>
internal class ParameterExtractor
{
    private readonly ReturnValueAnalyzer _returnValueAnalyzer;

    /// <summary>
    /// Initializes a new instance of ParameterExtractor with required dependencies.
    /// </summary>
    /// <param name="returnValueAnalyzer">Analyzer for determining return value types</param>
    public ParameterExtractor(ReturnValueAnalyzer returnValueAnalyzer)
    {
        _returnValueAnalyzer = returnValueAnalyzer ?? throw new System.ArgumentNullException(nameof(returnValueAnalyzer));
    }

    /// <summary>
    /// Analyzes data flow for the selected statements to determine parameters and return values.
    /// </summary>
    /// <param name="semanticModel">The semantic model for symbol resolution</param>
    /// <param name="method">The containing method declaration</param>
    /// <param name="statementsToExtract">The statements to be extracted</param>
    /// <param name="targetFramework">The target framework for compatibility checks</param>
    /// <returns>DataFlowInfo containing parameters and return value information</returns>
    public DataFlowInfo AnalyzeDataFlow(
        SemanticModel semanticModel,
        MethodDeclarationSyntax method,
        List<StatementSyntax> statementsToExtract,
        string? targetFramework)
    {
        // TODO: Extract from ExtractMethod.cs lines 232-313
        throw new System.NotImplementedException("To be extracted from ExtractMethod.cs");
    }

    /// <summary>
    /// Information about data flow for extracted methods.
    /// Contains parameters flowing in and return values flowing out.
    /// </summary>
    public class DataFlowInfo
    {
        public List<ParameterInfo> Parameters { get; set; } = new();
        public ITypeSymbol? ReturnType { get; set; }
        public bool IsVoid { get; set; }
        public bool IsTupleReturn { get; set; }
        public List<string> ReturnVariableNames { get; set; } = new();
    }

    /// <summary>
    /// Information about a single parameter for the extracted method.
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public ITypeSymbol Type { get; set; } = null!;
        public bool IsRef { get; set; }
    }
}
