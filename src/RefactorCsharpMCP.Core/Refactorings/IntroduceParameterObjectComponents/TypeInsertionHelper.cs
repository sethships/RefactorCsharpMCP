using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorCsharpMCP.Core.Refactorings.IntroduceParameterObjectComponents;

/// <summary>
/// Handles insertion of parameter object type declarations into the syntax tree.
/// Inserts new types at the correct location (before the target class, in namespace or at root).
/// </summary>
public class TypeInsertionHelper
{
    /// <summary>
    /// Inserts the parameter object class before the target class in the compilation unit.
    /// </summary>
    /// <param name="root">The compilation unit root.</param>
    /// <param name="targetClass">The class that contains the refactored method.</param>
    /// <param name="parameterObjectClass">The generated parameter object class/record.</param>
    /// <returns>The updated compilation unit with the inserted parameter object.</returns>
    public CompilationUnitSyntax InsertParameterObjectClass(
        CompilationUnitSyntax root,
        ClassDeclarationSyntax targetClass,
        MemberDeclarationSyntax parameterObjectClass)
    {
        // Find the namespace or use root
        var namespaceDecl = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (namespaceDecl != null)
        {
            // Find the target class in the namespace
            var classInNamespace = namespaceDecl.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == targetClass.Identifier.Text);

            if (classInNamespace != null)
            {
                // Insert before the class
                var index = namespaceDecl.Members.IndexOf(classInNamespace);
                if (index >= 0)
                {
                    var newMembers = namespaceDecl.Members.Insert(index, parameterObjectClass);
                    var newNamespace = namespaceDecl.WithMembers(newMembers);
                    return root.ReplaceNode(namespaceDecl, newNamespace);
                }
            }
        }

        // Insert at root level if no namespace
        var classAtRoot = root.Members.OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == targetClass.Identifier.Text);

        if (classAtRoot != null)
        {
            var classIndex = root.Members.IndexOf(classAtRoot);
            if (classIndex >= 0)
            {
                var rootMembers = root.Members.Insert(classIndex, parameterObjectClass);
                return root.WithMembers(rootMembers);
            }
        }

        // Fallback: append to end if we can't find the class
        // This should never happen in normal operation - indicates a bug in caller
        Debug.Assert(false,
            $"TypeInsertionHelper: Could not find target class '{targetClass.Identifier.Text}' in syntax tree. " +
            "This indicates the targetClass was not found in the provided root.");
        return root.WithMembers(root.Members.Add(parameterObjectClass));
    }
}
