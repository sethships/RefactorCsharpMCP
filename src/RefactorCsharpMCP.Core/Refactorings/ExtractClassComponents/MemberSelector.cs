using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Handles member name parsing, validation, and lookup for Extract Class refactoring.
/// </summary>
internal class MemberSelector
{
    internal List<string> ParseNames(string names)
    {
        return names
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
    }

    /// <summary>
    /// Validates that all specified members exist in the class and returns their syntax nodes.
    /// </summary>
    /// <param name="classDeclaration">The class declaration to search.</param>
    /// <param name="className">The class name for error messages.</param>
    /// <param name="fieldNames">Field names to find.</param>
    /// <param name="methodNames">Method names to find.</param>
    /// <param name="nestedTypeNames">Nested type names to find.</param>
    /// <param name="fieldNodes">Output parameter for found field declarations.</param>
    /// <param name="methodNodes">Output parameter for found method declarations.</param>
    /// <param name="nestedTypeNodes">Output parameter for found nested type declarations.</param>
    /// <returns>Null if all members found successfully, or RefactoringResult.Failure with error message.</returns>
    internal RefactoringResult? ValidateAndFindMembers(
        ClassDeclarationSyntax classDeclaration,
        string className,
        List<string> fieldNames,
        List<string> methodNames,
        List<string> nestedTypeNames,
        out List<FieldDeclarationSyntax> fieldNodes,
        out List<MethodDeclarationSyntax> methodNodes,
        out List<BaseTypeDeclarationSyntax> nestedTypeNodes)
    {
        fieldNodes = new List<FieldDeclarationSyntax>();
        methodNodes = new List<MethodDeclarationSyntax>();
        nestedTypeNodes = new List<BaseTypeDeclarationSyntax>();

        // Validate and find fields
        foreach (var fieldName in fieldNames)
        {
            var fieldDeclaration = FindFieldDeclaration(classDeclaration, fieldName);
            if (fieldDeclaration == null)
            {
                return RefactoringResult.Failure($"Field '{fieldName}' not found in class '{className}'.");
            }
            fieldNodes.Add(fieldDeclaration);
        }

        // Validate and find methods
        foreach (var methodName in methodNames)
        {
            var methodDeclaration = classDeclaration.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName);

            if (methodDeclaration == null)
            {
                return RefactoringResult.Failure($"Method '{methodName}' not found in class '{className}'.");
            }
            methodNodes.Add(methodDeclaration);
        }

        // Validate and find nested types (with unsupported delegate check)
        foreach (var typeName in nestedTypeNames)
        {
            // Check for unsupported delegate types
            var delegateDeclaration = classDeclaration.Members
                .OfType<DelegateDeclarationSyntax>()
                .FirstOrDefault(d => d.Identifier.Text == typeName);

            if (delegateDeclaration != null)
            {
                return RefactoringResult.Failure(
                    $"Nested delegate extraction is not supported. Attempted to extract delegate '{typeName}'. " +
                    $"Delegates inherit from BaseMethodDeclarationSyntax, not BaseTypeDeclarationSyntax, and require specialized handling.");
            }

            var nestedType = FindNestedType(classDeclaration, typeName);
            if (nestedType == null)
            {
                return RefactoringResult.Failure($"Nested type '{typeName}' not found in class '{className}'.");
            }
            nestedTypeNodes.Add(nestedType);
        }

        return null; // Success - all members found
    }

    /// <summary>
    /// Re-finds members in an updated class declaration after tree mutations.
    /// Used to obtain fresh syntax nodes from a mutated syntax tree.
    /// </summary>
    /// <param name="classDeclaration">The updated class declaration.</param>
    /// <param name="fieldNames">Field names to re-find.</param>
    /// <param name="methodNames">Method names to re-find.</param>
    /// <returns>Tuple of field and method syntax nodes found in the updated class.</returns>
    /// <remarks>
    /// This method does not validate - it assumes members exist (validation happens earlier).
    /// It silently skips members not found, which should not occur in normal flow.
    /// </remarks>
    internal (List<FieldDeclarationSyntax> Fields, List<MethodDeclarationSyntax> Methods) RefindMembersInUpdatedClass(
        ClassDeclarationSyntax classDeclaration,
        List<string> fieldNames,
        List<string> methodNames)
    {
        var fields = new List<FieldDeclarationSyntax>();
        var methods = new List<MethodDeclarationSyntax>();

        // Re-find fields
        foreach (var fieldName in fieldNames)
        {
            var field = FindFieldDeclaration(classDeclaration, fieldName);
            if (field != null)
            {
                fields.Add(field);
            }
        }

        // Re-find methods
        foreach (var methodName in methodNames)
        {
            var method = classDeclaration.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName);
            if (method != null)
            {
                methods.Add(method);
            }
        }

        return (fields, methods);
    }

    internal FieldDeclarationSyntax? FindFieldDeclaration(ClassDeclarationSyntax classDeclaration, string fieldName)
    {
        return classDeclaration.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));
    }

    /// <summary>
    /// Finds a nested type declaration by name.
    /// Uses BaseTypeDeclarationSyntax for polymorphism (supports class, struct, record, enum, interface).
    /// NOTE: Does not support nested delegate types as they inherit from BaseMethodDeclarationSyntax, not BaseTypeDeclarationSyntax.
    /// </summary>
    internal BaseTypeDeclarationSyntax? FindNestedType(ClassDeclarationSyntax classDeclaration, string typeName)
    {
        return classDeclaration.Members
            .OfType<BaseTypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.Text == typeName);
    }
}
