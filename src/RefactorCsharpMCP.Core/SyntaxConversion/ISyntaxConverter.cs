using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RefactorCsharpMCP.Core.SyntaxConversion;

/// <summary>
/// Defines the contract for syntax converters that transform modern C# features
/// to legacy-compatible equivalents for older frameworks.
/// </summary>
public interface ISyntaxConverter
{
    /// <summary>
    /// Gets the name of this converter (e.g., "CollectionExpressionConverter").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Determines whether this converter can process the given syntax node
    /// for the specified target framework.
    /// </summary>
    /// <param name="node">The syntax node to check.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net48", "net35").</param>
    /// <returns>True if this converter should process the node; otherwise, false.</returns>
    bool CanConvert(SyntaxNode node, string targetFramework);

    /// <summary>
    /// Converts the given syntax node to be compatible with the target framework.
    /// </summary>
    /// <param name="node">The syntax node to convert.</param>
    /// <param name="targetFramework">The target framework moniker.</param>
    /// <returns>The converted syntax node, or the original if no conversion is needed.</returns>
    SyntaxNode Convert(SyntaxNode node, string targetFramework);

    /// <summary>
    /// Gets the minimum C# language version required for the original syntax
    /// that this converter handles.
    /// </summary>
    LanguageVersion MinimumSourceLanguageVersion { get; }

    /// <summary>
    /// Gets the maximum C# language version that requires conversion
    /// (i.e., frameworks with this version or lower need conversion).
    /// </summary>
    LanguageVersion MaximumTargetLanguageVersion { get; }
}
