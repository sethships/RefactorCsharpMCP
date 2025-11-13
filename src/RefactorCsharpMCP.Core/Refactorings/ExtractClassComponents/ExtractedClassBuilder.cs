using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents.Strategies;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Builder for constructing extracted class declarations using the builder pattern.
/// Provides a fluent API for configuring and building the new class with appropriate modifiers,
/// members, and structure based on the extraction strategy.
/// </summary>
public class ExtractedClassBuilder
{
    private string? _className;
    private readonly List<FieldDeclarationSyntax> _fields = new();
    private readonly List<MethodDeclarationSyntax> _methods = new();
    private readonly List<BaseTypeDeclarationSyntax> _nestedTypes = new();
    private IExtractionModifierStrategy? _strategy;
    private ExtractionContext? _context;
    private SyntaxTriviaList _leadingTrivia = SyntaxFactory.TriviaList();
    private SyntaxTriviaList _trailingTrivia = SyntaxFactory.TriviaList();

    /// <summary>
    /// Sets the name of the extracted class.
    /// </summary>
    /// <param name="className">The name for the new class.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public ExtractedClassBuilder WithClassName(string className)
    {
        _className = className ?? throw new ArgumentNullException(nameof(className));
        return this;
    }

    /// <summary>
    /// Adds fields to be included in the extracted class.
    /// </summary>
    /// <param name="fields">The fields to extract.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public ExtractedClassBuilder WithFields(IEnumerable<FieldDeclarationSyntax> fields)
    {
        _fields.AddRange(fields ?? throw new ArgumentNullException(nameof(fields)));
        return this;
    }

    /// <summary>
    /// Adds methods to be included in the extracted class.
    /// </summary>
    /// <param name="methods">The methods to extract.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public ExtractedClassBuilder WithMethods(IEnumerable<MethodDeclarationSyntax> methods)
    {
        _methods.AddRange(methods ?? throw new ArgumentNullException(nameof(methods)));
        return this;
    }

    /// <summary>
    /// Adds nested types to be included in the extracted class.
    /// </summary>
    /// <param name="nestedTypes">The nested types to extract.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public ExtractedClassBuilder WithNestedTypes(IEnumerable<BaseTypeDeclarationSyntax> nestedTypes)
    {
        _nestedTypes.AddRange(nestedTypes ?? throw new ArgumentNullException(nameof(nestedTypes)));
        return this;
    }

    /// <summary>
    /// Sets the strategy for determining class and member modifiers.
    /// </summary>
    /// <param name="strategy">The extraction modifier strategy to use.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public ExtractedClassBuilder WithStrategy(IExtractionModifierStrategy strategy)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        return this;
    }

    /// <summary>
    /// Sets the extraction context for strategy decisions.
    /// </summary>
    /// <param name="context">The extraction context.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public ExtractedClassBuilder WithContext(ExtractionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        return this;
    }

    /// <summary>
    /// Sets the leading trivia (comments, whitespace) for the class declaration.
    /// </summary>
    /// <param name="trivia">The leading trivia to apply.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public ExtractedClassBuilder WithLeadingTrivia(SyntaxTriviaList trivia)
    {
        _leadingTrivia = trivia;
        return this;
    }

    /// <summary>
    /// Sets the trailing trivia (comments, whitespace) for the class declaration.
    /// </summary>
    /// <param name="trivia">The trailing trivia to apply.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public ExtractedClassBuilder WithTrailingTrivia(SyntaxTriviaList trivia)
    {
        _trailingTrivia = trivia;
        return this;
    }

    /// <summary>
    /// Builds the extracted class declaration using the configured settings.
    /// </summary>
    /// <returns>A <see cref="ClassDeclarationSyntax"/> representing the extracted class.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when required properties (ClassName, Strategy, Context) are not set.
    /// </exception>
    /// <remarks>
    /// The build process:
    /// <list type="number">
    /// <item><description>Validates required configuration (class name, strategy, context)</description></item>
    /// <item><description>Transforms fields using the strategy</description></item>
    /// <item><description>Transforms methods using the strategy</description></item>
    /// <item><description>Adds nested types (not transformed)</description></item>
    /// <item><description>Gets class modifiers from strategy</description></item>
    /// <item><description>Constructs final class declaration with all members</description></item>
    /// </list>
    /// </remarks>
    public ClassDeclarationSyntax Build()
    {
        // Validate required properties
        if (string.IsNullOrWhiteSpace(_className))
            throw new InvalidOperationException("Class name must be set before building.");

        if (_strategy == null)
            throw new InvalidOperationException("Strategy must be set before building.");

        if (_context == null)
            throw new InvalidOperationException("Context must be set before building.");

        var members = new List<MemberDeclarationSyntax>();

        // Transform fields using strategy
        var transformedFields = _fields.Select(f => _strategy.TransformFieldModifiers(f, _context));
        members.AddRange(transformedFields);

        // Transform methods using strategy
        var transformedMethods = _methods.Select(m => _strategy.TransformMethodModifiers(m, _context));
        members.AddRange(transformedMethods);

        // Add nested types (not transformed by strategy currently)
        members.AddRange(_nestedTypes);

        // Get class modifiers from strategy
        var classModifiers = _strategy.GetClassModifiers(_context);

        // Build the class declaration
        var classDecl = SyntaxFactory.ClassDeclaration(_className)
            .WithModifiers(classModifiers)
            .WithMembers(SyntaxFactory.List(members));

        // Apply trivia if specified
        if (_leadingTrivia.Count > 0)
            classDecl = classDecl.WithLeadingTrivia(_leadingTrivia);

        if (_trailingTrivia.Count > 0)
            classDecl = classDecl.WithTrailingTrivia(_trailingTrivia);

        return classDecl;
    }

    /// <summary>
    /// Resets the builder to its initial state, clearing all configured values.
    /// Useful for reusing the same builder instance for multiple class constructions.
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    public ExtractedClassBuilder Reset()
    {
        _className = null;
        _fields.Clear();
        _methods.Clear();
        _nestedTypes.Clear();
        _strategy = null;
        _context = null;
        _leadingTrivia = SyntaxFactory.TriviaList();
        _trailingTrivia = SyntaxFactory.TriviaList();
        return this;
    }
}
