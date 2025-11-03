using Microsoft.CodeAnalysis;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides consistent type name formatting for Roslyn symbols across refactorings.
/// Centralizes the SymbolDisplayFormat configuration to ensure type names are formatted
/// consistently in generated code.
/// </summary>
internal static class SymbolTypeFormatter
{
    /// <summary>
    /// Standard format for displaying fully-qualified type names in generated code.
    /// Uses namespace qualification, includes generic type parameters, and uses C# special types (int, string, etc.).
    /// </summary>
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.None,
        parameterOptions: SymbolDisplayParameterOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    /// <summary>
    /// Gets the fully-qualified type name from a symbol.
    /// Handles different symbol types (local variables, parameters, fields, properties).
    /// </summary>
    /// <param name="symbol">The symbol to get the type from.</param>
    /// <returns>Fully-qualified type name, or "object" if type cannot be determined.</returns>
    public static string GetSymbolType(ISymbol symbol)
    {
        var typeSymbol = symbol switch
        {
            ILocalSymbol local => local.Type,
            IParameterSymbol param => param.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol prop => prop.Type,
            _ => null
        };

        if (typeSymbol == null)
        {
            return "object";
        }

        var typeString = typeSymbol.ToDisplayString(FullyQualifiedFormat);
        return string.IsNullOrWhiteSpace(typeString) ? "object" : typeString;
    }
}
