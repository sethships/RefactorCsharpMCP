using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Provides context for extraction strategies to make informed decisions about modifiers and transformations.
/// Contains metadata about the source class, extracted members, and extraction mode for strategy selection.
/// </summary>
public class ExtractionContext
{
    /// <summary>
    /// Gets the source class declaration from which members are being extracted.
    /// </summary>
    public ClassDeclarationSyntax SourceClass { get; }

    /// <summary>
    /// Gets the name of the new class being created.
    /// </summary>
    public string NewClassName { get; }

    /// <summary>
    /// Gets the names of fields being extracted from the source class.
    /// </summary>
    public IReadOnlyList<string> ExtractedFieldNames { get; }

    /// <summary>
    /// Gets the names of methods being extracted from the source class.
    /// </summary>
    public IReadOnlyList<string> ExtractedMethodNames { get; }

    /// <summary>
    /// Gets the semantic model for the source code (may be null for syntax-only extraction).
    /// </summary>
    public SemanticModel? SemanticModel { get; }

    /// <summary>
    /// Gets the extraction mode that determines strategy selection.
    /// </summary>
    public ExtractionMode Mode { get; }

    /// <summary>
    /// Gets whether the extracted members include any public fields or methods.
    /// </summary>
    public bool HasPublicMembers { get; }

    /// <summary>
    /// Gets whether the extracted members include any protected fields or methods.
    /// </summary>
    public bool HasProtectedMembers { get; }

    /// <summary>
    /// Gets whether the source class is declared as public.
    /// </summary>
    public bool IsSourceClassPublic { get; }

    /// <summary>
    /// Gets whether the source class is declared as partial.
    /// </summary>
    public bool IsPartialClass { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionContext"/> class.
    /// </summary>
    /// <param name="sourceClass">The source class from which members are being extracted.</param>
    /// <param name="newClassName">The name of the new class to create.</param>
    /// <param name="extractedFieldNames">Names of fields being extracted.</param>
    /// <param name="extractedMethodNames">Names of methods being extracted.</param>
    /// <param name="semanticModel">Optional semantic model for advanced analysis.</param>
    /// <param name="mode">The extraction mode for strategy selection.</param>
    public ExtractionContext(
        ClassDeclarationSyntax sourceClass,
        string newClassName,
        IEnumerable<string> extractedFieldNames,
        IEnumerable<string> extractedMethodNames,
        SemanticModel? semanticModel = null,
        ExtractionMode mode = ExtractionMode.Default)
    {
        SourceClass = sourceClass ?? throw new ArgumentNullException(nameof(sourceClass));
        NewClassName = newClassName ?? throw new ArgumentNullException(nameof(newClassName));
        ExtractedFieldNames = (extractedFieldNames ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
        ExtractedMethodNames = (extractedMethodNames ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
        SemanticModel = semanticModel;
        Mode = mode;

        // Analyze source class characteristics
        HasPublicMembers = AnalyzePublicMembers(sourceClass, ExtractedFieldNames, ExtractedMethodNames);
        HasProtectedMembers = AnalyzeProtectedMembers(sourceClass, ExtractedFieldNames, ExtractedMethodNames);
        IsSourceClassPublic = sourceClass.Modifiers.Any(SyntaxKind.PublicKeyword);
        IsPartialClass = sourceClass.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static bool AnalyzePublicMembers(
        ClassDeclarationSyntax sourceClass,
        IReadOnlyList<string> fieldNames,
        IReadOnlyList<string> methodNames)
    {
        // Check if any extracted fields are public
        var publicFields = sourceClass.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.Modifiers.Any(SyntaxKind.PublicKeyword))
            .SelectMany(f => f.Declaration.Variables.Select(v => v.Identifier.Text))
            .Any(name => fieldNames.Contains(name));

        // Check if any extracted methods are public
        var publicMethods = sourceClass.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword))
            .Select(m => m.Identifier.Text)
            .Any(name => methodNames.Contains(name));

        return publicFields || publicMethods;
    }

    private static bool AnalyzeProtectedMembers(
        ClassDeclarationSyntax sourceClass,
        IReadOnlyList<string> fieldNames,
        IReadOnlyList<string> methodNames)
    {
        // Check if any extracted fields are protected
        var protectedFields = sourceClass.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.Modifiers.Any(SyntaxKind.ProtectedKeyword))
            .SelectMany(f => f.Declaration.Variables.Select(v => v.Identifier.Text))
            .Any(name => fieldNames.Contains(name));

        // Check if any extracted methods are protected
        var protectedMethods = sourceClass.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(SyntaxKind.ProtectedKeyword))
            .Select(m => m.Identifier.Text)
            .Any(name => methodNames.Contains(name));

        return protectedFields || protectedMethods;
    }
}

/// <summary>
/// Specifies the extraction mode for strategy selection.
/// Determines how modifiers are applied to the extracted class and its members.
/// </summary>
public enum ExtractionMode
{
    /// <summary>
    /// Use default behavior (internal composition for backward compatibility).
    /// The extracted class and methods are marked internal for composition pattern.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Force internal composition pattern.
    /// Explicitly requests internal visibility for encapsulated implementation details.
    /// </summary>
    InternalComposition = 1,

    /// <summary>
    /// Extract as public API.
    /// The extracted class is public, methods retain or upgrade to appropriate visibility.
    /// </summary>
    PublicApi = 2,

    /// <summary>
    /// Extract for inheritance (protected members).
    /// Prepares extracted class for inheritance scenarios with protected visibility.
    /// </summary>
    InheritanceReady = 3,

    /// <summary>
    /// Preserve original visibility of all members.
    /// No modifier transformations, maintains existing public/private/protected/internal.
    /// </summary>
    PreserveVisibility = 4,

    /// <summary>
    /// Let factory decide based on heuristics.
    /// Analyzes source class and members to select the most appropriate strategy automatically.
    /// </summary>
    Automatic = 5
}
