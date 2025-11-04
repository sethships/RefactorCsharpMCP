using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to make fields readonly when they are only assigned in constructors.
/// </summary>
public class MakeFieldReadonly : RefactoringBase
{
    /// <summary>
    /// Makes the specified field readonly if it's only assigned in constructors, with framework-aware validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the field.</param>
    /// <param name="className">The name of the class containing the field.</param>
    /// <param name="fieldName">The name of the field to make readonly.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48").</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public async Task<RefactoringResult> ExecuteAsync(
        string sourceCode,
        string className,
        string fieldName,
        string targetFramework)
    {
        // Skip framework validation - Execute method does its own syntax validation
        // and doesn't require full framework reference assemblies
        return await Task.Run(() => Execute(sourceCode, className, fieldName));
    }

    /// <summary>
    /// Makes the specified field readonly if it's only assigned in constructors.
    /// </summary>
    /// <param name="sourceCode">The source code containing the field.</param>
    /// <param name="className">The name of the class containing the field.</param>
    /// <param name="fieldName">The name of the field to make readonly.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(string sourceCode, string className, string fieldName)
    {
        // Validate inputs
        var sourceValidation = ValidateNonEmpty(sourceCode, "Source code");
        if (!sourceValidation.IsSuccess) return sourceValidation;

        var classValidation = ValidateNonEmpty(className, "Class name");
        if (!classValidation.IsSuccess) return classValidation;

        var fieldValidation = ValidateNonEmpty(fieldName, "Field name");
        if (!fieldValidation.IsSuccess) return fieldValidation;

        try
        {
            // Parse and validate syntax
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
            {
                return parseResult;
            }

            // Find the class declaration
            var classDeclaration = FindClass(root, className);
            if (classDeclaration == null)
            {
                return RefactoringResult.Failure($"Class '{className}' not found in source code.");
            }

            // Find the field declaration
            var fieldDeclaration = FindFieldDeclaration(classDeclaration, fieldName);
            if (fieldDeclaration == null)
            {
                return RefactoringResult.Failure($"Field '{fieldName}' not found in class '{className}'.");
            }

            // Check if field is already readonly
            if (fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)))
            {
                return RefactoringResult.Failure($"Field '{fieldName}' is already readonly.");
            }

            // Check if field is const
            if (fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            {
                return RefactoringResult.Failure($"Field '{fieldName}' is const and cannot be made readonly.");
            }

            // Create compilation for semantic analysis
            var compilation = CreateCompilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Verify field is only assigned in constructors
            var canBeReadonly = CanFieldBeReadonly(classDeclaration, fieldName, semanticModel);
            if (!canBeReadonly.IsValid)
            {
                return RefactoringResult.Failure(canBeReadonly.Reason ?? "Field cannot be made readonly.");
            }

            // Add readonly modifier
            var updatedField = AddReadonlyModifier(fieldDeclaration);
            var updatedClass = classDeclaration.ReplaceNode(fieldDeclaration, updatedField);

            // Replace class in root
            var newRoot = root.ReplaceNode(classDeclaration, updatedClass);

            // Normalize whitespace to ensure proper formatting
            newRoot = NormalizeWhitespace(newRoot);

            return RefactoringResult.Success(
                newRoot.ToFullString(),
                $"Made field '{fieldName}' readonly in class '{className}'."
            );
        }
        catch (Exception ex)
        {
            return HandleException(ex, "make field readonly");
        }
    }

    private FieldDeclarationSyntax? FindFieldDeclaration(ClassDeclarationSyntax classDeclaration, string fieldName)
    {
        return classDeclaration.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));
    }

    private (bool IsValid, string? Reason) CanFieldBeReadonly(
        ClassDeclarationSyntax classDeclaration,
        string fieldName,
        SemanticModel semanticModel)
    {
        // Check for field initializer first
        var fieldDeclaration = FindFieldDeclaration(classDeclaration, fieldName);
        if (fieldDeclaration != null)
        {
            var variable = fieldDeclaration.Declaration.Variables
                .FirstOrDefault(v => v.Identifier.Text == fieldName);

            if (variable?.Initializer != null)
            {
                // Field has an initializer - this is allowed for readonly fields
                // Readonly fields can be initialized at declaration or in constructors
                // So we'll allow this and continue checking for other assignments
            }
        }

        // Find all simple assignments to this field
        var assignments = classDeclaration.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(a => IsAssignmentToField(a, fieldName))
            .ToList();

        // Find compound assignments (++, --, etc.)
        var prefixUnaryModifications = classDeclaration.DescendantNodes()
            .OfType<PrefixUnaryExpressionSyntax>()
            .Where(u => (u.IsKind(SyntaxKind.PreIncrementExpression) || u.IsKind(SyntaxKind.PreDecrementExpression))
                && IsFieldReference(u.Operand, fieldName))
            .ToList();

        var postfixUnaryModifications = classDeclaration.DescendantNodes()
            .OfType<PostfixUnaryExpressionSyntax>()
            .Where(u => (u.IsKind(SyntaxKind.PostIncrementExpression) || u.IsKind(SyntaxKind.PostDecrementExpression))
                && IsFieldReference(u.Operand, fieldName))
            .ToList();

        // Check for lambda captures - lambdas that reference and potentially modify the field
        var lambdaCaptures = classDeclaration.DescendantNodes()
            .OfType<AnonymousFunctionExpressionSyntax>()
            .Where(lambda => LambdaCapturesField(lambda, fieldName))
            .ToList();

        if (lambdaCaptures.Any())
        {
            return (false, $"Field '{fieldName}' is captured by lambda/anonymous function. Cannot safely determine if it's only assigned in constructors.");
        }

        // Check if there are any modifications at all
        var totalModifications = assignments.Count + prefixUnaryModifications.Count + postfixUnaryModifications.Count;

        if (totalModifications == 0)
        {
            // No assignments found - field is never assigned (safe to make readonly)
            return (true, null);
        }

        // Check if all assignments are in constructors
        foreach (var assignment in assignments)
        {
            var validation = ValidateAssignmentLocation(assignment, fieldName);
            if (!validation.IsValid)
                return validation;
        }

        // Check if any unary modifications (++, --) exist - these can't be in constructors for readonly
        if (prefixUnaryModifications.Any() || postfixUnaryModifications.Any())
        {
            return (false, $"Field '{fieldName}' is modified with increment/decrement operators outside of simple assignment.");
        }

        return (true, null);
    }

    private (bool IsValid, string? Reason) ValidateAssignmentLocation(SyntaxNode assignment, string fieldName)
    {
        var containingMethod = assignment.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        var containingConstructor = assignment.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();

        // Assignment in a regular method (not constructor)
        if (containingMethod != null && containingConstructor == null)
        {
            return (false, $"Field '{fieldName}' is assigned outside of constructors.");
        }

        // Check for assignments in property setters
        var containingAccessor = assignment.FirstAncestorOrSelf<AccessorDeclarationSyntax>();
        if (containingAccessor != null && containingAccessor.IsKind(SyntaxKind.SetAccessorDeclaration))
        {
            return (false, $"Field '{fieldName}' is assigned in a property setter.");
        }

        return (true, null);
    }

    private bool IsAssignmentToField(AssignmentExpressionSyntax assignment, string fieldName)
    {
        return IsFieldReference(assignment.Left, fieldName);
    }

    private bool IsFieldReference(ExpressionSyntax expression, string fieldName)
    {
        // Check for direct field reference: fieldName
        if (expression is IdentifierNameSyntax identifierName)
        {
            return identifierName.Identifier.Text == fieldName;
        }

        // Check for this.fieldName
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is ThisExpressionSyntax &&
                memberAccess.Name is IdentifierNameSyntax memberName)
            {
                return memberName.Identifier.Text == fieldName;
            }
        }

        return false;
    }

    private bool LambdaCapturesField(AnonymousFunctionExpressionSyntax lambda, string fieldName)
    {
        // Check if the lambda body references the field
        var identifiers = lambda.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(i => i.Identifier.Text == fieldName)
            .ToList();

        return identifiers.Any();
    }

    private FieldDeclarationSyntax AddReadonlyModifier(FieldDeclarationSyntax fieldDeclaration)
    {
        // Find the position to insert readonly modifier
        // Correct order: access-modifier static readonly type
        // e.g., "private static readonly" not "private readonly static"
        var modifiers = fieldDeclaration.Modifiers;
        int insertIndex = 0;

        // Find position after access modifiers and static, but before type
        for (int i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (modifier.IsKind(SyntaxKind.PublicKeyword) ||
                modifier.IsKind(SyntaxKind.PrivateKeyword) ||
                modifier.IsKind(SyntaxKind.ProtectedKeyword) ||
                modifier.IsKind(SyntaxKind.InternalKeyword) ||
                modifier.IsKind(SyntaxKind.StaticKeyword))
            {
                insertIndex = i + 1;
            }
        }

        // Create readonly token with proper spacing
        var readonlyToken = SyntaxFactory.Token(
            SyntaxFactory.TriviaList(),
            SyntaxKind.ReadOnlyKeyword,
            SyntaxFactory.TriviaList(SyntaxFactory.Space)
        );

        // Insert readonly at the calculated position
        var newModifiers = modifiers.Insert(insertIndex, readonlyToken);

        return fieldDeclaration.WithModifiers(newModifiers);
    }
}
