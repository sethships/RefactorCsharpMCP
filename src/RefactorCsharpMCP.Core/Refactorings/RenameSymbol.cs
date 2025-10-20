using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.Utilities;
using System.Text.RegularExpressions;

namespace RefactorCsharpMCP.Core.Refactorings;

/// <summary>
/// Provides functionality to rename symbols (local variables, parameters, private methods, private fields)
/// throughout a single file using position-based resolution.
/// </summary>
public class RenameSymbol : RefactoringBase
{
    private readonly SymbolResolutionHelper _symbolHelper = new();

    // Regex pattern for valid C# identifiers (compiled for performance)
    private static readonly Regex IdentifierRegex = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Renames a symbol at the specified position with framework-aware validation.
    /// </summary>
    /// <param name="sourceCode">The source code containing the symbol to rename.</param>
    /// <param name="lineNumber">The 1-based line number of the symbol.</param>
    /// <param name="columnNumber">The 1-based column number of the symbol.</param>
    /// <param name="newName">The new identifier name.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., "net8.0", "net48").</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public async Task<RefactoringResult> ExecuteAsync(
        string sourceCode,
        int lineNumber,
        int columnNumber,
        string newName,
        string targetFramework)
    {
        return await ExecuteWithValidationAsync(
            sourceCode,
            targetFramework,
            async () => await Task.Run(() => Execute(sourceCode, lineNumber, columnNumber, newName)));
    }

    /// <summary>
    /// Renames a symbol at the specified position.
    /// </summary>
    /// <param name="sourceCode">The source code containing the symbol to rename.</param>
    /// <param name="lineNumber">The 1-based line number of the symbol.</param>
    /// <param name="columnNumber">The 1-based column number of the symbol.</param>
    /// <param name="newName">The new identifier name.</param>
    /// <returns>A result containing the refactored code or error information.</returns>
    public RefactoringResult Execute(
        string sourceCode,
        int lineNumber,
        int columnNumber,
        string newName)
    {
        // Validate inputs
        var sourceValidation = ValidateNonEmpty(sourceCode, "Source code");
        if (!sourceValidation.IsSuccess) return sourceValidation;

        var nameValidation = ValidateNonEmpty(newName, "New name");
        if (!nameValidation.IsSuccess) return nameValidation;

        if (lineNumber < 1 || columnNumber < 1)
        {
            return RefactoringResult.Failure(
                $"Invalid position: line {lineNumber}, column {columnNumber}. Line and column numbers must be >= 1.");
        }

        // Validate new name is a valid C# identifier
        if (!IsValidIdentifier(newName))
        {
            return RefactoringResult.Failure(
                $"'{newName}' is not a valid C# identifier. Identifiers must start with a letter or underscore, followed by letters, digits, or underscores.");
        }

        try
        {
            // Parse and validate syntax FIRST
            CurrentPhase = "Syntax Parsing";
            var parseResult = ParseAndValidateSyntax(sourceCode, out var root, out var syntaxTree);
            if (!parseResult.IsSuccess || root == null || syntaxTree == null)
            {
                return parseResult;
            }

            // Create compilation for semantic analysis
            CurrentPhase = "Semantic Analysis";
            var compilation = CreateCompilation(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Get symbol at position using our compilation
            CurrentPhase = "Symbol Resolution";

            // Convert 1-based line/column to 0-based for Roslyn
            var text = syntaxTree.GetText();
            var linePosition = new Microsoft.CodeAnalysis.Text.LinePosition(lineNumber - 1, columnNumber - 1);
            int position;
            try
            {
                position = text.Lines.GetPosition(linePosition);
            }
            catch
            {
                return RefactoringResult.Failure($"Position line {lineNumber}, column {columnNumber} is out of range.");
            }

            // Find the syntax node at this position
            var node = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0));
            if (node == null)
            {
                return RefactoringResult.Failure($"No syntax node found at line {lineNumber}, column {columnNumber}.");
            }

            // Try to get symbol info
            var symbolInfo = semanticModel.GetSymbolInfo(node);
            var symbol = symbolInfo.Symbol;

            if (symbol == null)
            {
                // Try getting declared symbol if it's a declaration
                symbol = semanticModel.GetDeclaredSymbol(node);
            }

            if (symbol == null)
            {
                return RefactoringResult.Failure($"No symbol found at line {lineNumber}, column {columnNumber}.");
            }

            var originalName = symbol.Name;

            // Check if new name is the same as original
            if (originalName == newName)
            {
                return RefactoringResult.Failure($"Symbol is already named '{newName}'. No changes needed.");
            }

            // Validate symbol type (only support local, parameter, field, method for V1)
            var scopeInfo = _symbolHelper.AnalyzeSymbolScope(symbol);
            if (!IsSymbolSupportedForRename(scopeInfo))
            {
                return RefactoringResult.Failure(
                    $"Cannot rename {GetSymbolKindDescription(symbol)}: Only local variables, parameters, private fields, and private methods can be renamed in single-file scope.");
            }

            // Determine scope for conflict detection
            var scopeNode = DetermineScopeNode(node);
            if (scopeNode == null)
            {
                return RefactoringResult.Failure("Could not determine symbol scope for conflict detection.");
            }

            // Check for naming conflicts
            CurrentPhase = "Conflict Detection";
            var conflictResult = _symbolHelper.FindSymbolConflicts(semanticModel, newName, scopeNode);
            if (conflictResult.HasConflicts)
            {
                return RefactoringResult.Failure(conflictResult.ConflictDescription ?? "Name conflicts with existing symbols.");
            }

            // Find all references to the symbol
            CurrentPhase = "Reference Finding";
            var references = _symbolHelper.GetAllReferences(symbol, compilation);

            // Also include the declaration node itself
            var declarationLocation = symbol.Locations.FirstOrDefault(loc => loc.IsInSource && loc.SourceTree == syntaxTree);
            if (declarationLocation != null && !references.Any(r => r.SourceSpan == declarationLocation.SourceSpan))
            {
                references.Add(declarationLocation);
            }

            if (references.Count == 0)
            {
                Logger?.LogWarning("No references found for symbol '{OriginalName}' at line {Line}, column {Column}",
                    originalName, lineNumber, columnNumber);
            }

            // Replace all references with new name
            CurrentPhase = "Code Transformation";
            var newRoot = ReplaceSymbolReferences(root, references, newName, originalName, syntaxTree);

            // Normalize whitespace to ensure proper formatting
            newRoot = NormalizeWhitespace(newRoot);

            var referenceCount = references.Count;
            var message = referenceCount > 0
                ? $"Renamed '{originalName}' to '{newName}' ({referenceCount} reference{(referenceCount == 1 ? "" : "s")} updated)."
                : $"Renamed '{originalName}' to '{newName}' (declaration only, no references found).";

            return RefactoringResult.Success(newRoot.ToFullString(), message);
        }
        catch (Exception ex)
        {
            return HandleException(ex, "rename symbol");
        }
    }

    /// <summary>
    /// Validates that a name is a valid C# identifier.
    /// </summary>
    private bool IsValidIdentifier(string name)
    {
        return IdentifierRegex.IsMatch(name);
    }

    /// <summary>
    /// Determines if a symbol is supported for renaming in V1 (single-file scope).
    /// </summary>
    private bool IsSymbolSupportedForRename(SymbolScopeInfo scopeInfo)
    {
        // Support local variables, parameters, private fields, and private methods
        if (scopeInfo.IsLocal || scopeInfo.IsParameter)
        {
            return true;
        }

        if (scopeInfo.IsField || scopeInfo.IsMethod || scopeInfo.IsProperty)
        {
            // Only support private members for single-file scope
            return scopeInfo.IsPrivate;
        }

        return false;
    }

    /// <summary>
    /// Gets a human-readable description of a symbol's kind.
    /// </summary>
    private string GetSymbolKindDescription(ISymbol symbol)
    {
        return symbol.Kind switch
        {
            SymbolKind.Local => "local variable",
            SymbolKind.Parameter => "parameter",
            SymbolKind.Field => $"{symbol.DeclaredAccessibility.ToString().ToLower()} field",
            SymbolKind.Method => $"{symbol.DeclaredAccessibility.ToString().ToLower()} method",
            SymbolKind.Property => $"{symbol.DeclaredAccessibility.ToString().ToLower()} property",
            _ => symbol.Kind.ToString().ToLower()
        };
    }

    /// <summary>
    /// Determines the appropriate scope node for conflict detection.
    /// </summary>
    private SyntaxNode? DetermineScopeNode(SyntaxNode node)
    {
        // For local variables and parameters, use the containing method
        var methodScope = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodScope != null) return methodScope;

        var constructorScope = node.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        if (constructorScope != null) return constructorScope;

        // For fields and methods, use the containing class
        var classScope = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classScope != null) return classScope;

        // Fallback to the root compilation unit
        return node.FirstAncestorOrSelf<CompilationUnitSyntax>();
    }

    /// <summary>
    /// Replaces all references to a symbol with a new name.
    /// </summary>
    private CompilationUnitSyntax ReplaceSymbolReferences(
        CompilationUnitSyntax root,
        List<Location> references,
        string newName,
        string originalName,
        SyntaxTree syntaxTree)
    {
        // Create a dictionary of nodes to replace
        var nodesToReplace = new Dictionary<SyntaxNode, SyntaxNode>();

        foreach (var reference in references)
        {
            if (reference.SourceTree != syntaxTree) continue; // Only process current tree

            var node = root.FindNode(reference.SourceSpan);

            // Handle different node types
            if (TryGetNodeToReplace(node, originalName, newName, out var oldNode, out var newNode))
            {
                if (!nodesToReplace.ContainsKey(oldNode))
                {
                    nodesToReplace[oldNode] = newNode;
                }
            }
        }

        // Apply all replacements
        if (nodesToReplace.Count == 0)
        {
            return root; // No changes needed
        }

        var newRoot = root.ReplaceNodes(nodesToReplace.Keys, (original, _) => nodesToReplace[original]);
        return newRoot;
    }

    /// <summary>
    /// Tries to get the node to replace and its replacement for a given reference location.
    /// </summary>
    private bool TryGetNodeToReplace(
        SyntaxNode node,
        string originalName,
        string newName,
        out SyntaxNode oldNode,
        out SyntaxNode newNode)
    {
        oldNode = null!;
        newNode = null!;

        // Direct identifier name (usages)
        if (node is IdentifierNameSyntax identifierName && identifierName.Identifier.Text == originalName)
        {
            oldNode = identifierName;
            newNode = SyntaxFactory.IdentifierName(newName).WithTriviaFrom(identifierName);
            return true;
        }

        // Variable declarator (local variable declarations)
        // The location might point to just the identifier token, so we need to find the parent declarator
        var variableDeclarator = node as VariableDeclaratorSyntax ?? node.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
        if (variableDeclarator != null && variableDeclarator.Identifier.Text == originalName)
        {
            oldNode = variableDeclarator;
            newNode = variableDeclarator.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(variableDeclarator.Identifier));
            return true;
        }

        // Parameter (method/lambda parameters)
        var parameter = node as ParameterSyntax ?? node.FirstAncestorOrSelf<ParameterSyntax>();
        if (parameter != null && parameter.Identifier.Text == originalName)
        {
            oldNode = parameter;
            newNode = parameter.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(parameter.Identifier));
            return true;
        }

        // Method declaration (method names)
        var methodDeclaration = node as MethodDeclarationSyntax ?? node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration != null && methodDeclaration.Identifier.Text == originalName)
        {
            oldNode = methodDeclaration;
            newNode = methodDeclaration.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(methodDeclaration.Identifier));
            return true;
        }

        // Field declaration - need to find the variable declarator within it
        var fieldDeclaration = node.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (fieldDeclaration != null)
        {
            var fieldDeclarator = fieldDeclaration.Declaration.Variables
                .FirstOrDefault(v => v.Identifier.Text == originalName);
            if (fieldDeclarator != null)
            {
                oldNode = fieldDeclarator;
                newNode = fieldDeclarator.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(fieldDeclarator.Identifier));
                return true;
            }
        }

        // Single variable designation (deconstruction declarations like: var (x, y) = ...)
        var designation = node as SingleVariableDesignationSyntax ?? node.FirstAncestorOrSelf<SingleVariableDesignationSyntax>();
        if (designation != null && designation.Identifier.Text == originalName)
        {
            oldNode = designation;
            newNode = designation.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(designation.Identifier));
            return true;
        }

        // If the node contains an identifier, try to find it
        var containedIdentifier = node.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .FirstOrDefault(i => i.Identifier.Text == originalName);

        if (containedIdentifier != null)
        {
            oldNode = containedIdentifier;
            newNode = SyntaxFactory.IdentifierName(newName).WithTriviaFrom(containedIdentifier);
            return true;
        }

        return false;
    }
}
