using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

namespace RefactorCsharpMCP.Core.SyntaxConversion;

/// <summary>
/// Abstract base class for all syntax converters, providing common functionality
/// for trivia preservation and framework compatibility checking.
/// </summary>
public abstract class SyntaxConverterBase : CSharpSyntaxRewriter, ISyntaxConverter
{
    /// <summary>
    /// Gets the name of this converter.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the minimum C# language version required for the original syntax.
    /// </summary>
    public abstract LanguageVersion MinimumSourceLanguageVersion { get; }

    /// <summary>
    /// Gets the maximum C# language version that requires conversion.
    /// </summary>
    public abstract LanguageVersion MaximumTargetLanguageVersion { get; }

    /// <summary>
    /// Gets the target framework for this conversion pass.
    /// </summary>
    protected string TargetFramework { get; private set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyntaxConverterBase"/> class.
    /// Configures the rewriter to visit into structured trivia to preserve formatting.
    /// </summary>
    protected SyntaxConverterBase() : base(visitIntoStructuredTrivia: false)
    {
    }

    /// <summary>
    /// Determines whether this converter should process the given node for the target framework.
    /// </summary>
    public virtual bool CanConvert(SyntaxNode node, string targetFramework)
    {
        // Get the language version for the target framework
        var targetLanguageVersion = FrameworkMoniker.GetLanguageVersion(targetFramework);

        // Only convert if the target framework has an older language version than the source syntax requires
        return targetLanguageVersion <= MaximumTargetLanguageVersion;
    }

    /// <summary>
    /// Converts the given syntax node to be compatible with the target framework.
    /// </summary>
    public SyntaxNode Convert(SyntaxNode node, string targetFramework)
    {
        TargetFramework = targetFramework;

        // Only perform conversion if needed for this framework
        if (!CanConvert(node, targetFramework))
        {
            return node;
        }

        // Visit the node tree and perform conversions
        var converted = Visit(node);
        return converted ?? node;
    }

    /// <summary>
    /// Preserves trivia (whitespace, comments) from the original node to the converted node.
    /// </summary>
    /// <typeparam name="T">The type of syntax node.</typeparam>
    /// <param name="converted">The converted syntax node.</param>
    /// <param name="original">The original syntax node.</param>
    /// <returns>The converted node with trivia from the original.</returns>
    protected T PreserveTrivia<T>(T converted, SyntaxNode original) where T : SyntaxNode
    {
        return converted
            .WithLeadingTrivia(original.GetLeadingTrivia())
            .WithTrailingTrivia(original.GetTrailingTrivia());
    }

    /// <summary>
    /// Checks if the target framework supports a specific C# language version.
    /// </summary>
    /// <param name="requiredVersion">The required language version.</param>
    /// <returns>True if the target framework supports the version; otherwise, false.</returns>
    protected bool TargetSupports(LanguageVersion requiredVersion)
    {
        var targetVersion = FrameworkMoniker.GetLanguageVersion(TargetFramework);
        return targetVersion >= requiredVersion;
    }

    /// <summary>
    /// Gets the C# language version for the target framework.
    /// </summary>
    protected LanguageVersion TargetLanguageVersion =>
        FrameworkMoniker.GetLanguageVersion(TargetFramework);
}
