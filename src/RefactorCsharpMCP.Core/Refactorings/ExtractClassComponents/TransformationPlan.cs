using Microsoft.CodeAnalysis.Text;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Contains metadata for all transformations to be applied during Extract Class refactoring.
/// This pattern separates analysis (using SemanticModel on original tree) from transformation
/// (location-based application), solving the Roslyn Identity Paradox.
/// </summary>
/// <remarks>
/// <para><strong>Architecture Pattern</strong>: Transform Planning Pattern</para>
/// <para>Phase 1 (Analysis): Collect transformation metadata using original SemanticModel</para>
/// <para>Phase 2 (Transform): Apply transformations using location-based matching (TextSpan)</para>
/// <para>Phase 3 (Extract): Final structural changes via ExtractClassTransformer</para>
///
/// <para>This solves Issue #124 by avoiding semantic model usage on modified trees.</para>
/// </remarks>
internal class TransformationPlan
{
    /// <summary>
    /// Type qualifications for field and variable declarations.
    /// Maps type identifier location (TextSpan) to qualification information.
    /// Example: Config → Configuration.Config
    /// </summary>
    public Dictionary<TextSpan, TypeQualificationInfo> TypeQualifications { get; set; } = new();

    /// <summary>
    /// Locations of member access expressions that need transformation.
    /// Example: this._field → _compositionField._field
    /// </summary>
    public HashSet<TextSpan> MemberAccessLocations { get; set; } = new();

    /// <summary>
    /// Locations of method invocations that need transformation.
    /// Example: MethodName() → _compositionField.MethodName()
    /// </summary>
    public HashSet<TextSpan> MethodInvocationLocations { get; set; } = new();

    /// <summary>
    /// Locations of simple identifier references that need transformation.
    /// Example: identifier → _compositionField.identifier
    /// </summary>
    public HashSet<TextSpan> IdentifierReferenceLocations { get; set; } = new();

    /// <summary>
    /// Locations of qualified type name usages that need transformation.
    /// Example: OriginalClass.NestedType → NewClass.NestedType
    /// </summary>
    public Dictionary<TextSpan, QualifiedNameTransformation> QualifiedNameTransformations { get; set; } = new();
}

/// <summary>
/// Information about a type identifier that needs to be qualified.
/// </summary>
internal class TypeQualificationInfo
{
    /// <summary>
    /// Location of the type identifier in the source code.
    /// </summary>
    public TextSpan Location { get; set; }

    /// <summary>
    /// Original unqualified type name (e.g., "Config").
    /// </summary>
    public string OriginalTypeName { get; set; } = string.Empty;

    /// <summary>
    /// New class name to use for qualification (e.g., "Configuration").
    /// </summary>
    public string NewClassName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a field declaration.
    /// </summary>
    public bool IsFieldDeclaration { get; set; }

    /// <summary>
    /// Whether this is a local variable declaration.
    /// </summary>
    public bool IsLocalVariable { get; set; }

    /// <summary>
    /// Whether this is a property type.
    /// </summary>
    public bool IsPropertyType { get; set; }

    /// <summary>
    /// Whether this is a return type.
    /// </summary>
    public bool IsReturnType { get; set; }

    /// <summary>
    /// Whether this is a parameter type.
    /// </summary>
    public bool IsParameterType { get; set; }
}

/// <summary>
/// Information about a qualified name that needs transformation.
/// Example: OriginalClass.NestedType → NewClass.NestedType
/// </summary>
internal class QualifiedNameTransformation
{
    /// <summary>
    /// Location of the qualified name in the source code.
    /// </summary>
    public TextSpan Location { get; set; }

    /// <summary>
    /// Original left side of the qualified name (e.g., "OriginalClass").
    /// </summary>
    public string OriginalLeft { get; set; } = string.Empty;

    /// <summary>
    /// New left side for the qualified name (e.g., "NewClass").
    /// </summary>
    public string NewLeft { get; set; } = string.Empty;

    /// <summary>
    /// Right side of the qualified name (e.g., "NestedType").
    /// Remains unchanged during transformation.
    /// </summary>
    public string Right { get; set; } = string.Empty;
}
