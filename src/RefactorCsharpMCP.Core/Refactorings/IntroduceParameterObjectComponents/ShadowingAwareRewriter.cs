using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Utilities;

namespace RefactorCsharpMCP.Core.Refactorings.IntroduceParameterObjectComponents;

/// <summary>
/// Syntax rewriter that replaces parameter references with parameter object property access.
/// Uses name-based matching to avoid SyntaxTree identity issues after transformations.
/// Tracks shadowed names during tree traversal to correctly handle local declarations
/// (catch variables, foreach variables, lambda parameters, pattern matching, etc.)
/// that shadow the original method parameters.
///
/// Handles 9+ shadowing scenarios:
/// 1. Catch clause variables
/// 2. ForEach loop variables
/// 3. Simple lambda parameters
/// 4. Parenthesized lambda parameters
/// 5. Local function parameters
/// 6. For loop variables
/// 7. Using statement variables
/// 8. Pattern matching variables (if, switch expression, switch section)
/// 9. LINQ range variables (from, let, join, into)
///
/// <para>
/// <strong>Note on Local Variable Shadowing:</strong>
/// This class does NOT implement <c>VisitLocalDeclarationStatement</c> for local variable shadowing
/// because the C# compiler already prevents declaring a local variable with the same name as a parameter
/// (CS0136: "A local or parameter named 'x' cannot be declared in this scope"). The <see cref="IsDeclarationIdentifier"/>
/// method provides additional defensive protection by skipping transformation of declaration identifiers.
/// </para>
/// </summary>
public class ShadowingAwareRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _parameterNames;
    private readonly string _paramObjectName;
    private readonly HashSet<string> _shadowedNames = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the ShadowingAwareRewriter.
    /// </summary>
    /// <param name="parameterSymbols">The parameter symbols being grouped into the parameter object.</param>
    /// <param name="paramObjectName">The name of the parameter object variable (camelCase).</param>
    public ShadowingAwareRewriter(
        List<IParameterSymbol> parameterSymbols,
        string paramObjectName)
    {
        // Extract parameter names from symbols BEFORE tree transformations
        _parameterNames = new HashSet<string>(
            parameterSymbols.Select(p => p.Name),
            StringComparer.Ordinal);
        _paramObjectName = paramObjectName;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var identName = node.Identifier.Text;

        // Skip if this parameter name is currently shadowed by a local declaration
        if (_shadowedNames.Contains(identName))
        {
            return base.VisitIdentifierName(node);
        }

        // Use name-based matching instead of semantic model to avoid SyntaxTree identity issues
        if (_parameterNames.Contains(identName))
        {
            // Scope validation: Skip transformation if this identifier IS a declaration itself
            if (IsDeclarationIdentifier(node))
            {
                return base.VisitIdentifierName(node);
            }

            // Replace parameter reference with property access
            var propertyName = NamingHelper.ToPascalCase(identName);
            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(_paramObjectName),
                SyntaxFactory.IdentifierName(propertyName));
        }

        return base.VisitIdentifierName(node);
    }

    /// <summary>
    /// Determines if the identifier IS the declaration itself (not a reference within scope).
    /// </summary>
    private bool IsDeclarationIdentifier(IdentifierNameSyntax node)
    {
        var parent = node.Parent;

        while (parent != null)
        {
            switch (parent)
            {
                // Variable declaration: string name = ...
                case VariableDeclaratorSyntax declarator:
                    if (declarator.Identifier.Text == node.Identifier.Text)
                        return true;
                    break;

                // Stop at statement/expression level
                case StatementSyntax:
                case ExpressionSyntax when parent is not MemberAccessExpressionSyntax:
                    return false;
            }

            parent = parent.Parent;
        }

        return false;
    }

    public override SyntaxNode? VisitCatchClause(CatchClauseSyntax node)
    {
        var catchVarName = node.Declaration?.Identifier.Text;
        if (catchVarName != null && _parameterNames.Contains(catchVarName))
        {
            _shadowedNames.Add(catchVarName);
            var result = base.VisitCatchClause(node);
            _shadowedNames.Remove(catchVarName);
            return result;
        }
        return base.VisitCatchClause(node);
    }

    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        var varName = node.Identifier.Text;
        if (_parameterNames.Contains(varName))
        {
            _shadowedNames.Add(varName);
            var result = base.VisitForEachStatement(node);
            _shadowedNames.Remove(varName);
            return result;
        }
        return base.VisitForEachStatement(node);
    }

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        var paramName = node.Parameter.Identifier.Text;
        if (_parameterNames.Contains(paramName))
        {
            _shadowedNames.Add(paramName);
            var result = base.VisitSimpleLambdaExpression(node);
            _shadowedNames.Remove(paramName);
            return result;
        }
        return base.VisitSimpleLambdaExpression(node);
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        // Collect all lambda parameters that shadow method parameters
        var shadowingParams = node.ParameterList.Parameters
            .Select(p => p.Identifier.Text)
            .Where(name => _parameterNames.Contains(name))
            .ToList();

        if (shadowingParams.Count > 0)
        {
            foreach (var name in shadowingParams)
                _shadowedNames.Add(name);

            var result = base.VisitParenthesizedLambdaExpression(node);

            foreach (var name in shadowingParams)
                _shadowedNames.Remove(name);

            return result;
        }
        return base.VisitParenthesizedLambdaExpression(node);
    }

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        // Collect all local function parameters that shadow method parameters
        var shadowingParams = node.ParameterList.Parameters
            .Select(p => p.Identifier.Text)
            .Where(name => _parameterNames.Contains(name))
            .ToList();

        if (shadowingParams.Count > 0)
        {
            foreach (var name in shadowingParams)
                _shadowedNames.Add(name);

            var result = base.VisitLocalFunctionStatement(node);

            foreach (var name in shadowingParams)
                _shadowedNames.Remove(name);

            return result;
        }
        return base.VisitLocalFunctionStatement(node);
    }

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        // Collect all for loop variables that shadow method parameters
        var shadowingVars = new List<string>();
        if (node.Declaration != null)
        {
            foreach (var variable in node.Declaration.Variables)
            {
                var varName = variable.Identifier.Text;
                if (_parameterNames.Contains(varName))
                    shadowingVars.Add(varName);
            }
        }

        if (shadowingVars.Count > 0)
        {
            foreach (var name in shadowingVars)
                _shadowedNames.Add(name);

            var result = base.VisitForStatement(node);

            foreach (var name in shadowingVars)
                _shadowedNames.Remove(name);

            return result;
        }
        return base.VisitForStatement(node);
    }

    public override SyntaxNode? VisitUsingStatement(UsingStatementSyntax node)
    {
        // Collect all using statement variables that shadow method parameters
        var shadowingVars = new List<string>();
        if (node.Declaration != null)
        {
            foreach (var variable in node.Declaration.Variables)
            {
                var varName = variable.Identifier.Text;
                if (_parameterNames.Contains(varName))
                    shadowingVars.Add(varName);
            }
        }

        if (shadowingVars.Count > 0)
        {
            foreach (var name in shadowingVars)
                _shadowedNames.Add(name);

            var result = base.VisitUsingStatement(node);

            foreach (var name in shadowingVars)
                _shadowedNames.Remove(name);

            return result;
        }
        return base.VisitUsingStatement(node);
    }

    public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
    {
        // For if statements with pattern matching in the condition, the pattern variable's
        // scope extends to the entire if body (then branch), not just the pattern expression.
        // Example: if (value is string value) { Console.WriteLine(value.Length); }
        // Here 'value' inside the block refers to the pattern variable, not the parameter.

        var shadowingVars = CollectPatternVariablesFromExpression(node.Condition);

        if (shadowingVars.Count > 0)
        {
            // Visit condition first (pattern is defined here)
            var newCondition = (ExpressionSyntax?)Visit(node.Condition);

            // Add shadowing for the statement body
            foreach (var name in shadowingVars)
                _shadowedNames.Add(name);

            // Visit the statement body with shadowing active
            var newStatement = (StatementSyntax?)Visit(node.Statement);

            // Remove shadowing before visiting else clause (pattern vars not in scope there)
            foreach (var name in shadowingVars)
                _shadowedNames.Remove(name);

            // Visit else clause without the pattern variable shadowing
            var newElse = node.Else != null ? (ElseClauseSyntax?)Visit(node.Else) : null;

            return node
                .WithCondition(newCondition ?? node.Condition)
                .WithStatement(newStatement ?? node.Statement)
                .WithElse(newElse);
        }

        return base.VisitIfStatement(node);
    }

    public override SyntaxNode? VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
    {
        // Collect variables from switch arm pattern that shadow method parameters
        var shadowingVars = CollectPatternVariables(node.Pattern);

        if (shadowingVars.Count > 0)
        {
            foreach (var name in shadowingVars)
                _shadowedNames.Add(name);

            var result = base.VisitSwitchExpressionArm(node);

            foreach (var name in shadowingVars)
                _shadowedNames.Remove(name);

            return result;
        }
        return base.VisitSwitchExpressionArm(node);
    }

    public override SyntaxNode? VisitSwitchSection(SwitchSectionSyntax node)
    {
        // Collect all pattern variables from case labels in this section
        // Pattern variables' scope extends to the entire switch section (case body),
        // not just the label, so we must shadow at the section level.
        var shadowingVars = new List<string>();
        foreach (var label in node.Labels.OfType<CasePatternSwitchLabelSyntax>())
        {
            shadowingVars.AddRange(CollectPatternVariables(label.Pattern));
        }

        if (shadowingVars.Count > 0)
        {
            foreach (var name in shadowingVars)
                _shadowedNames.Add(name);

            var result = base.VisitSwitchSection(node);

            foreach (var name in shadowingVars)
                _shadowedNames.Remove(name);

            return result;
        }
        return base.VisitSwitchSection(node);
    }

    public override SyntaxNode? VisitQueryExpression(QueryExpressionSyntax node)
    {
        // LINQ query expressions introduce range variables whose scope extends to the
        // entire query body. We need to collect all such variables upfront and shadow
        // them for the entire query expression.
        var shadowingVars = CollectLinqRangeVariables(node);

        if (shadowingVars.Count > 0)
        {
            foreach (var name in shadowingVars)
                _shadowedNames.Add(name);

            var result = base.VisitQueryExpression(node);

            foreach (var name in shadowingVars)
                _shadowedNames.Remove(name);

            return result;
        }
        return base.VisitQueryExpression(node);
    }

    /// <summary>
    /// Collects all range variables introduced in a LINQ query expression that shadow method parameters.
    /// This includes from clauses, let clauses, join clauses (including into), and query continuations.
    /// Optimized to filter during collection rather than after, reducing allocations for queries
    /// where most range variables don't shadow method parameters.
    /// </summary>
    private List<string> CollectLinqRangeVariables(QueryExpressionSyntax query)
    {
        var rangeVars = new List<string>();

        // Collect from the initial from clause (filter during collection for performance)
        var fromName = query.FromClause.Identifier.Text;
        if (_parameterNames.Contains(fromName))
            rangeVars.Add(fromName);

        // Collect from body clauses and continuations (also filters during collection)
        CollectLinqRangeVariablesFromBody(query.Body, rangeVars);

        return rangeVars;
    }

    private void CollectLinqRangeVariablesFromBody(QueryBodySyntax body, List<string> rangeVars)
    {
        // Collect from body clauses (from, let, join, where, orderby)
        // Filter during collection to avoid post-processing allocation
        foreach (var clause in body.Clauses)
        {
            switch (clause)
            {
                case FromClauseSyntax fromClause:
                    var fromName = fromClause.Identifier.Text;
                    if (_parameterNames.Contains(fromName))
                        rangeVars.Add(fromName);
                    break;
                case LetClauseSyntax letClause:
                    var letName = letClause.Identifier.Text;
                    if (_parameterNames.Contains(letName))
                        rangeVars.Add(letName);
                    break;
                case JoinClauseSyntax joinClause:
                    var joinName = joinClause.Identifier.Text;
                    if (_parameterNames.Contains(joinName))
                        rangeVars.Add(joinName);
                    if (joinClause.Into != null)
                    {
                        var intoName = joinClause.Into.Identifier.Text;
                        if (_parameterNames.Contains(intoName))
                            rangeVars.Add(intoName);
                    }
                    break;
            }
        }

        // Collect from query continuation (into ... select/group)
        if (body.Continuation != null)
        {
            var contName = body.Continuation.Identifier.Text;
            if (_parameterNames.Contains(contName))
                rangeVars.Add(contName);
            CollectLinqRangeVariablesFromBody(body.Continuation.Body, rangeVars);
        }
    }

    /// <summary>
    /// Collects pattern variables from an expression (e.g., if condition with is pattern).
    /// </summary>
    private List<string> CollectPatternVariablesFromExpression(ExpressionSyntax expression)
    {
        var result = new List<string>();

        // Find all IsPatternExpression nodes in the expression
        foreach (var isPattern in expression.DescendantNodesAndSelf().OfType<IsPatternExpressionSyntax>())
        {
            CollectPatternVariablesRecursive(isPattern.Pattern, result);
        }

        // Filter in-place to avoid additional allocation
        result.RemoveAll(name => !_parameterNames.Contains(name));
        return result;
    }

    /// <summary>
    /// Recursively collects variable names from pattern syntax nodes.
    /// </summary>
    private List<string> CollectPatternVariables(PatternSyntax pattern)
    {
        var result = new List<string>();
        CollectPatternVariablesRecursive(pattern, result);
        // Filter in-place to avoid additional allocation
        result.RemoveAll(name => !_parameterNames.Contains(name));
        return result;
    }

    private void CollectPatternVariablesRecursive(PatternSyntax pattern, List<string> result)
    {
        switch (pattern)
        {
            case DeclarationPatternSyntax declPattern:
                if (declPattern.Designation is SingleVariableDesignationSyntax singleVar)
                    result.Add(singleVar.Identifier.Text);
                break;

            case VarPatternSyntax varPattern:
                if (varPattern.Designation is SingleVariableDesignationSyntax varSingleVar)
                    result.Add(varSingleVar.Identifier.Text);
                break;

            case RecursivePatternSyntax recursivePattern:
                if (recursivePattern.Designation is SingleVariableDesignationSyntax recVar)
                    result.Add(recVar.Identifier.Text);
                if (recursivePattern.PropertyPatternClause != null)
                {
                    foreach (var subPattern in recursivePattern.PropertyPatternClause.Subpatterns)
                    {
                        if (subPattern.Pattern != null)
                            CollectPatternVariablesRecursive(subPattern.Pattern, result);
                    }
                }
                if (recursivePattern.PositionalPatternClause != null)
                {
                    foreach (var subPattern in recursivePattern.PositionalPatternClause.Subpatterns)
                    {
                        if (subPattern.Pattern != null)
                            CollectPatternVariablesRecursive(subPattern.Pattern, result);
                    }
                }
                break;

            case BinaryPatternSyntax binaryPattern:
                CollectPatternVariablesRecursive(binaryPattern.Left, result);
                CollectPatternVariablesRecursive(binaryPattern.Right, result);
                break;

            case ParenthesizedPatternSyntax parenPattern:
                CollectPatternVariablesRecursive(parenPattern.Pattern, result);
                break;

            case UnaryPatternSyntax unaryPattern:
                CollectPatternVariablesRecursive(unaryPattern.Pattern, result);
                break;
        }
    }
}
