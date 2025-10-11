# Architectural Review: PRD V1 Refactoring Capabilities

**Document Version:** 1.0
**Review Date:** 2025-10-09
**Reviewer:** Master Software Architect
**PRD Version Reviewed:** 1.0.0
**Related PR:** [#14 - Add PRD for V1 Refactoring Capabilities](https://github.com/sethb75/RefactorCsharpMCP/pull/14)

---

## 1. Executive Summary

### Overall Assessment: **APPROVE WITH RECOMMENDATIONS**

The PRD for V1 Refactoring Capabilities is **technically sound and well-researched**. The product owner has done excellent work prioritizing based on industry research and aligning with AI-assisted development workflows. The proposed refactorings are achievable with Roslyn APIs, and the phasing strategy is pragmatic.

### Key Strengths
- ✅ **Excellent research foundation** - Industry data on refactoring frequency is accurate
- ✅ **Pragmatic scope management** - Single-file limitation is appropriate for V1
- ✅ **Strong alignment with existing architecture** - Builds on proven patterns
- ✅ **Clear documentation of limitations** - OUT OF SCOPE sections prevent scope creep
- ✅ **Realistic prioritization** - P0/P1/P2 tiers match technical dependencies

### Areas Requiring Attention
- ⚠️ **Rename complexity underestimated** - Symbol resolution more complex than PRD suggests
- ⚠️ **Extract Method return value detection** - Significant Roslyn API complexity
- ⚠️ **Inline operations edge cases** - More challenging than "P1" priority indicates
- ⚠️ **Testing strategy undefined** - 90% coverage target needs implementation plan
- ⚠️ **Performance validation missing** - 2-second target requires benchmarking framework

### Recommendation
**Approve PRD with minor adjustments to Phase 1 timeline and addition of spike work for Rename refactoring.** Consider extending Phase 1 from 2 weeks to 3 weeks to account for Rename complexity.

---

## 2. Detailed Refactoring Analysis

### 2.1 Rename Symbol (P0 - NEW)

#### Technical Feasibility: ⚠️ **Feasible with Challenges**

**Required Roslyn APIs:**
- `SemanticModel.GetSymbolInfo()` - Symbol resolution at cursor position
- `SymbolFinder.FindReferencesAsync()` - Find all symbol references (async API)
- `ISymbol` analysis - Distinguish local/parameter/field/method symbols
- `SyntaxNode.ReplaceToken()` - Token-level replacement for identifiers
- Conflict detection - `SemanticModel.LookupSymbols()` for name collision checking

**Implementation Complexity:** **Medium-High** (underestimated as P0 "simple")

**Key Technical Challenges:**

1. **Symbol Resolution Ambiguity**
   - Problem: Roslyn requires exact position to identify symbol
   - Current API: Tools accept `symbolName` string, but multiple symbols may match
   - Solution: Add `lineNumber` and `columnNumber` to API for precise symbol location
   - Example: Field `_name` vs parameter `name` - same identifier, different symbols

2. **Scope-Aware Renaming**
   - Local variables: Simple - only within method scope
   - Parameters: Medium - rename in signature and body
   - Private fields: Medium - rename in all methods of class
   - Private methods: Complex - rename declaration and all call sites
   - Challenge: Each has different scope analysis requirements

3. **Name Conflict Detection**
   - Must check new name doesn't conflict with:
     - Existing symbols in scope (fields, methods, parameters, locals)
     - Base class members (for private methods/fields)
     - C# keywords and contextual keywords
   - Roslyn API: `SemanticModel.LookupSymbols()` at each reference location

4. **Async API Integration**
   - Problem: `SymbolFinder.FindReferencesAsync()` is async
   - Current architecture: All refactorings are synchronous
   - Solutions:
     - A) Make `Execute()` async (breaking change to all refactorings)
     - B) Use `.GetAwaiter().GetResult()` (acceptable for MCP stdio transport)
     - C) Implement manual reference finding (reinventing Roslyn)
   - Recommendation: **Option B** - Sync wrapper is acceptable for single-file scope

**Recommended Implementation Approach:**

```csharp
public class RenameSymbol
{
    public RefactoringResult Execute(
        string sourceCode,
        int lineNumber,        // NEW: Required for symbol resolution
        int columnNumber,      // NEW: Required for symbol resolution
        string newName)
    {
        // 1. Parse and create semantic model
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CreateCompilation(tree);
        var model = compilation.GetSemanticModel(tree);

        // 2. Find symbol at position
        var position = tree.GetText().Lines[lineNumber - 1].Start + columnNumber - 1;
        var node = tree.GetRoot().FindToken(position).Parent;
        var symbol = model.GetSymbolInfo(node).Symbol;

        if (symbol == null)
            return RefactoringResult.Failure("No symbol found at position");

        // 3. Validate symbol type (only support local/param/private field/method)
        if (!IsSupportedSymbolType(symbol))
            return RefactoringResult.Failure($"Cannot rename {symbol.Kind}");

        // 4. Check for name conflicts
        if (HasNameConflict(model, symbol, newName))
            return RefactoringResult.Failure($"Name '{newName}' conflicts with existing symbol");

        // 5. Find all references (sync wrapper for async API)
        var references = SymbolFinder.FindReferencesAsync(symbol, compilation.Solution)
            .GetAwaiter().GetResult();

        // 6. Replace all occurrences
        // ... token replacement logic
    }
}
```

**Estimated Implementation Effort:** **5-7 days** (not 3-4 days as P0 suggests)

**Testing Requirements:**
- Symbol resolution edge cases (nested scopes, shadowing)
- Conflict detection (all symbol types)
- Reference finding accuracy (member access, qualified names)
- Identifier escaping (`@class` keyword identifiers)
- Unicode identifiers (valid in C# but rare)

**Recommendations:**
1. **Add spike task** (2 days) to prototype symbol resolution before Phase 1
2. **Extend Phase 1 timeline** from 2 weeks to 3 weeks
3. **Update MCP tool signature** to include `lineNumber` and `columnNumber`
4. **Document limitation:** "Only supports symbols defined in the same file"

---

### 2.2 Extract Method - Return Value Detection (P0 - ENHANCEMENT)

#### Technical Feasibility: ✅ **Feasible**

**Required Roslyn APIs:**
- `DataFlowAnalysis.DataFlowsOut` - Already used in current implementation
- `DataFlowAnalysis.VariablesDeclared` - Identify variables declared in selection
- `DataFlowAnalysis.WrittenInside` - Variables assigned in selection
- Return type inference - Use `ITypeSymbol` from semantic model

**Implementation Complexity:** **Medium**

**Current Implementation Status:**
- ✅ Data flow analysis working (line 186-239 in ExtractMethod.cs)
- ✅ Parameter detection complete
- ❌ Return value detection commented as "enhancement" (line 283)
- ❌ Only void methods supported

**Key Technical Challenges:**

1. **Multiple Return Values**
   - Single return: Easy - use variable type
   - Multiple returns: Use tuple `(Type1, Type2)` - C# 7.0+
   - Challenge: Tuple element naming for clarity
   - Example: `(decimal total, decimal tax)` vs `(decimal, decimal)`

2. **Return Statement Generation**
   - Must add `return` statement at end of extracted method
   - Must capture return value at call site: `var result = ExtractedMethod();`
   - Challenge: If multiple values, destructure tuple: `var (total, tax) = Calculate();`

3. **Control Flow Analysis**
   - Early returns within selection - NOT SUPPORTED in V1 (documented)
   - Multiple exit points - Defer to V2
   - Exception handling - Defer to V2

**Recommended Implementation:**

```csharp
// In AnalyzeDataFlow():
private DataFlowInfo AnalyzeDataFlow(...)
{
    // ... existing parameter detection ...

    // NEW: Return value detection
    var outputVars = analysis.DataFlowsOut
        .Where(symbol => !analysis.VariablesDeclared.Contains(symbol))
        .Where(symbol => symbol is ILocalSymbol)
        .ToList();

    if (outputVars.Count == 0)
    {
        dataFlow.ReturnType = "void";
    }
    else if (outputVars.Count == 1)
    {
        var symbol = outputVars.First();
        dataFlow.ReturnType = GetSymbolType(symbol);
        dataFlow.ReturnVariableName = symbol.Name;
    }
    else
    {
        // Multiple return values - use tuple
        dataFlow.ReturnType = $"({string.Join(", ", outputVars.Select(v => GetSymbolType(v)))})";
        dataFlow.ReturnVariableNames = outputVars.Select(v => v.Name).ToList();
    }

    return dataFlow;
}

// Update BuildExtractedMethod():
private MethodDeclarationSyntax BuildExtractedMethod(...)
{
    // Change from hardcoded void to actual return type
    var returnType = string.IsNullOrEmpty(dataFlowInfo.ReturnType) || dataFlowInfo.ReturnType == "void"
        ? SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword))
        : SyntaxFactory.ParseTypeName(dataFlowInfo.ReturnType);

    // Add return statement if needed
    if (dataFlowInfo.ReturnType != "void")
    {
        var returnStmt = dataFlowInfo.ReturnVariableNames.Count == 1
            ? SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName(dataFlowInfo.ReturnVariableName))
            : SyntaxFactory.ReturnStatement(
                SyntaxFactory.TupleExpression(
                    SyntaxFactory.SeparatedList(
                        dataFlowInfo.ReturnVariableNames.Select(n =>
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(n)))
                    )
                )
            );

        allStatements = allStatements.Add(returnStmt);
    }

    // ... rest of method generation
}
```

**Estimated Implementation Effort:** **3-4 days**

**Testing Requirements:**
- Single return value (primitive types, reference types, generics)
- Multiple return values (tuple syntax)
- No return values (existing void tests)
- Variable shadowing edge cases

**Recommendations:**
1. **Limit to simple cases in V1:** No early returns, no exception handling
2. **Add validation:** Detect unsupported patterns and fail gracefully
3. **Document limitation:** "V1 supports single exit point only"

---

### 2.3 Inline Variable (P1 - NEW)

#### Technical Feasibility: ✅ **Feasible**

**Required Roslyn APIs:**
- `SemanticModel.GetDeclaredSymbol()` - Get variable symbol
- `SymbolFinder.FindReferencesAsync()` - Find all variable uses
- `SyntaxNode.ReplaceNode()` - Replace variable uses with initializer
- `SyntaxNode.RemoveNode()` - Remove variable declaration
- Parenthesis analysis - Ensure operator precedence preserved

**Implementation Complexity:** **Low-Medium**

**Key Technical Challenges:**

1. **Expression Complexity**
   - Simple literals: `var x = 5;` → inline as `5`
   - Method calls: `var x = GetValue();` → inline as `GetValue()`
   - Complex expressions: `var x = a + b * c;` → need parentheses: `(a + b * c)`
   - Side effects: `var x = counter++;` → CANNOT inline (mutates state)

2. **Multiple References**
   - Single use: Simple replacement
   - Multiple uses: Only safe if expression is pure (no side effects)
   - Example: `var x = GetRandom(); Use(x); Use(x);` → Cannot inline (different values)

3. **Parenthesis Insertion**
   - Must preserve operator precedence
   - Example: `var x = 2 + 3; var y = x * 4;` → `var y = (2 + 3) * 4;` not `2 + 3 * 4`
   - Roslyn has `SyntaxFactory.ParenthesizedExpression()`

4. **Side Effect Detection**
   - Must detect if expression has side effects
   - Safe: Literals, field access, local variable access
   - Unsafe: Method calls (may have side effects), increment/decrement, assignments
   - Conservative approach: V1 only allows literals and simple member access

**Recommended Implementation:**

```csharp
public class InlineVariable
{
    public RefactoringResult Execute(string sourceCode, string variableName, int lineNumber)
    {
        // 1. Find variable declaration
        var declaration = FindVariableDeclaration(root, variableName, lineNumber);
        if (declaration?.Initializer == null)
            return RefactoringResult.Failure("Variable must have initializer");

        // 2. Check for side effects (conservative)
        if (HasSideEffects(declaration.Initializer.Value))
            return RefactoringResult.Failure("Expression has side effects - cannot inline");

        // 3. Find all references
        var symbol = model.GetDeclaredSymbol(declaration);
        var references = FindReferences(symbol);

        // 4. For multiple uses, verify expression is pure
        if (references.Count > 1 && !IsPureExpression(declaration.Initializer.Value))
            return RefactoringResult.Failure("Expression not pure - cannot inline multiple uses");

        // 5. Replace each reference with (potentially parenthesized) expression
        var expression = declaration.Initializer.Value;
        var updatedRoot = root;

        foreach (var reference in references)
        {
            var needsParentheses = RequiresParentheses(reference, expression);
            var replacement = needsParentheses
                ? SyntaxFactory.ParenthesizedExpression(expression)
                : expression;

            updatedRoot = updatedRoot.ReplaceNode(reference, replacement);
        }

        // 6. Remove variable declaration
        updatedRoot = updatedRoot.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia);

        return RefactoringResult.Success(updatedRoot.ToFullString(), ...);
    }

    private bool IsPureExpression(ExpressionSyntax expr)
    {
        return expr switch
        {
            LiteralExpressionSyntax => true,
            IdentifierNameSyntax => true,
            MemberAccessExpressionSyntax => true,
            _ => false  // Conservative: reject complex expressions in V1
        };
    }
}
```

**Estimated Implementation Effort:** **4-5 days**

**Testing Requirements:**
- Literal inlining (int, string, bool, null)
- Simple variable reference inlining
- Multiple usage scenarios
- Parenthesis insertion correctness
- Side effect detection (reject method calls, increments)

**Recommendations:**
1. **Start conservative:** V1 only supports literals and variable references
2. **Add clear error messages:** "Expression may have side effects"
3. **Document limitation:** "V1 does not inline method calls or complex expressions"

---

### 2.4 Inline Method (P1 - NEW)

#### Technical Feasibility: ⚠️ **Feasible with Challenges**

**Required Roslyn APIs:**
- `SemanticModel.GetDeclaredSymbol()` - Get method symbol
- `SymbolFinder.FindReferencesAsync()` - Find all call sites
- `SyntaxNode.ReplaceNode()` - Replace method calls with body
- Parameter substitution - Replace parameter references with arguments
- Variable renaming - Avoid name conflicts with call site locals

**Implementation Complexity:** **High** (higher than P1 suggests)

**Key Technical Challenges:**

1. **Parameter Substitution**
   - Must replace parameter uses with actual arguments
   - Challenge: Arguments are expressions, not simple variables
   - Example: `Sum(a + b, c * d)` inlined needs `(a + b) + (c * d)`

2. **Variable Name Conflicts**
   - Method body may declare locals that conflict with call site
   - Example:
     ```csharp
     void Caller() {
         var temp = 5;
         Helper(temp);  // Helper also declares 'temp'
     }
     void Helper(int x) {
         var temp = x * 2;  // CONFLICT if inlined!
         Use(temp);
     }
     ```
   - Solution: Rename conflicting variables in inlined body

3. **Control Flow Complexity**
   - Simple methods: Just statements
   - Early returns: Cannot inline into expression context
   - Multiple returns: Must use temporary variable
   - Loops: Supported but complex
   - V1 Scope: **Only single-return methods** (documented limitation)

4. **Multiple Call Sites**
   - Must inline at every call site
   - Each may have different argument expressions
   - Must track and update all locations

**Recommended Implementation:**

```csharp
public class InlineMethod
{
    public RefactoringResult Execute(string sourceCode, string className, string methodName)
    {
        // 1. Find method declaration
        var method = FindMethod(root, className, methodName);

        // 2. Validate method is inlineable (V1: no early returns, no loops)
        if (!IsSimpleMethod(method))
            return RefactoringResult.Failure("Method too complex for V1 inlining");

        // 3. Find all call sites
        var symbol = model.GetDeclaredSymbol(method);
        var callSites = FindCallSites(symbol);

        if (callSites.Count == 0)
            return RefactoringResult.Failure("Method has no callers - use Safe Delete instead");

        // 4. Inline at each call site
        var updatedRoot = root;

        foreach (var callSite in callSites)
        {
            var inlinedStatements = InlineMethodBody(
                method,
                callSite,
                out var conflicts
            );

            // Rename conflicting variables
            inlinedStatements = RenameConflicts(inlinedStatements, conflicts);

            // Replace call with inlined statements
            updatedRoot = updatedRoot.ReplaceNode(
                callSite.Parent,  // Replace statement containing call
                inlinedStatements
            );
        }

        // 5. Remove method declaration
        updatedRoot = updatedRoot.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);

        return RefactoringResult.Success(updatedRoot.ToFullString(), ...);
    }

    private bool IsSimpleMethod(MethodDeclarationSyntax method)
    {
        if (method.Body == null) return false;

        // V1: No early returns
        var returnCount = method.Body.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .Count();

        if (returnCount > 1) return false;

        // V1: No loops
        if (method.Body.DescendantNodes().OfType<WhileStatementSyntax>().Any()) return false;
        if (method.Body.DescendantNodes().OfType<ForStatementSyntax>().Any()) return false;

        // V1: No out/ref parameters
        if (method.ParameterList.Parameters.Any(p =>
            p.Modifiers.Any(m => m.IsKind(SyntaxKind.OutKeyword) || m.IsKind(SyntaxKind.RefKeyword))))
            return false;

        return true;
    }
}
```

**Estimated Implementation Effort:** **6-8 days** (significantly more than typical P1)

**Testing Requirements:**
- Simple method inlining (no parameters, no returns)
- Parameter substitution (various argument types)
- Variable conflict detection and renaming
- Multiple call site handling
- Rejection of complex methods (early returns, loops)

**Recommendations:**
1. **Consider moving to P2 or Phase 2:** Complexity is high for Phase 1 timeline
2. **Start with simplest cases:** No parameters, no return, no local variables
3. **Document extensive limitations:** V1 is intentionally simple
4. **Alternative:** Defer entire feature to V1.5

---

### 2.5 Make Field Readonly (P1 - STABLE)

#### Technical Feasibility: ✅ **Feasible - Already Implemented**

**Current Implementation:** Solid, well-tested (27 tests covering various scenarios)

**No architectural concerns** - implementation is complete and follows best practices.

**Recommendation:** **No changes needed** - mark as stable in PRD

---

### 2.6 Safe Delete (P1 - DOCUMENT LIMITATIONS)

#### Technical Feasibility: ✅ **Feasible - Already Implemented**

**Current Implementation:** Working, with clear single-file limitation documented

**Architectural Observation:**
- Current approach (single-file reference checking) is pragmatic
- Warning messages to users are essential - **VERIFY THEY EXIST IN TOOL**

**Recommendations:**
1. **Verify MCP tool warns about cross-file limitation** in response message
2. **Add prominent warning in tool description:** "WARNING: Only checks references in current file"
3. **Consider future enhancement:** Add "Search" recommendation in error message
   - Example: "No references found in this file. Run project-wide search for '{methodName}' before deleting."

---

### 2.7 Extract Class (P1 - DOCUMENT LIMITATIONS)

#### Technical Feasibility: ✅ **Feasible - Already Implemented**

**Current Implementation:** Core extraction logic working

**Critical Issue:** PRD states "V1 does NOT automatically update references" (line 220)

**Architectural Concern:** This creates **significant manual work** for users and may lead to **broken code**.

**Recommendations:**
1. **Phase 2 Enhancement:** Add basic reference updating within same class
   - Replace `field.Method()` with `_extractedClass.Method()`
   - Replace `field` with `_extractedClass.Field`
   - This is achievable with Roslyn `SyntaxRewriter` pattern

2. **Interim Solution:** Generate TODO comments at reference locations
   ```csharp
   // TODO: Update reference - was 'field', now '_address.Field'
   var x = field;
   ```

3. **Tool Warning:** Return warning list of all references that need manual updates

**Estimated Effort for Reference Updates:** **3-4 days** - recommend adding to Phase 2

---

### 2.8 Constructor Injection (P0 - STABLE)

#### Technical Feasibility: ✅ **Feasible - Already Implemented**

**Current Implementation:** Excellent - handles merging with existing constructors, supports fields and properties

**No architectural concerns** - implementation is mature and well-tested.

**Recommendation:** **No changes needed**

---

### 2.9 Introduce Parameter Object (P2 - NEW)

#### Technical Feasibility: ✅ **Feasible**

**Required Roslyn APIs:**
- Method parameter analysis (existing)
- Class generation with `SyntaxFactory.ClassDeclaration()`
- Property generation (similar to Constructor Injection)
- Method signature rewriting
- Caller update - **Most complex part**

**Implementation Complexity:** **Medium-High**

**Key Technical Challenges:**

1. **Caller Updates**
   - Must find all callers of the method
   - Must update each call site to construct parameter object
   - Example:
     ```csharp
     // Before
     CreateCustomer("John", "john@example.com", "123 Main St", "Seattle", "98101");

     // After
     CreateCustomer("John", "john@example.com",
         new AddressInfo("123 Main St", "Seattle", "98101"));
     ```

2. **Parameter Grouping**
   - How does user specify which parameters to group?
   - Options:
     - A) User provides parameter indices: `[2, 3, 4]`
     - B) User provides parameter names: `["street", "city", "zip"]`
     - C) Tool suggests groupings based on naming patterns
   - Recommendation: **Option B** - explicit parameter names

3. **Class Placement**
   - V1: Generate class in same file
   - Challenge: Where to insert? Before or after containing class?
   - Recommendation: After containing class (simpler)

**Estimated Implementation Effort:** **5-6 days**

**Recommendation:** **Defer to Phase 4 or V1.1** - Not critical path, complex caller updates

---

### 2.10 Remove Unused Usings (P2 - NEW)

#### Technical Feasibility: ✅ **Feasible**

**Required Roslyn APIs:**
- `CompilationUnitSyntax.Usings` - Get all using directives
- `SemanticModel` - Check if types from namespace are used
- `SyntaxNode.RemoveNode()` - Remove unused usings

**Implementation Complexity:** **Low**

**This is the easiest refactoring to implement** - simpler than P1 operations.

**Recommended Implementation:**

```csharp
public class RemoveUnusedUsings
{
    public RefactoringResult Execute(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CreateCompilation(tree);
        var model = compilation.GetSemanticModel(tree);
        var root = (CompilationUnitSyntax)tree.GetRoot();

        var usedNamespaces = new HashSet<string>();

        // Find all type references in code
        var typeNodes = root.DescendantNodes().OfType<IdentifierNameSyntax>();
        foreach (var typeNode in typeNodes)
        {
            var symbolInfo = model.GetSymbolInfo(typeNode);
            if (symbolInfo.Symbol is INamedTypeSymbol typeSymbol)
            {
                var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
                if (!string.IsNullOrEmpty(ns))
                    usedNamespaces.Add(ns);
            }
        }

        // Remove unused using directives
        var usingsToRemove = root.Usings
            .Where(u => !usedNamespaces.Contains(u.Name.ToString()))
            .ToList();

        var updatedRoot = root.RemoveNodes(usingsToRemove, SyntaxRemoveOptions.KeepNoTrivia);

        return RefactoringResult.Success(
            updatedRoot.ToFullString(),
            $"Removed {usingsToRemove.Count} unused using directive(s)."
        );
    }
}
```

**Estimated Implementation Effort:** **2-3 days**

**Recommendation:** **Move to Phase 3** - Easy win, good for morale after complex refactorings

---

## 3. Cross-Cutting Concerns

### 3.1 Shared Infrastructure Needs

**Current Pattern Analysis:**
All refactorings follow a common structure:

```csharp
public RefactoringResult Execute(params...)
{
    // 1. Validate inputs
    if (invalid) return RefactoringResult.Failure("...");

    // 2. Parse source code
    var tree = CSharpSyntaxTree.ParseText(sourceCode);
    var root = tree.GetRoot();

    // 3. Check for syntax errors
    var errors = tree.GetDiagnostics().Where(d => d.Severity == Error);
    if (errors.Any()) return RefactoringResult.Failure("...");

    // 4. Create compilation + semantic model
    var compilation = CSharpCompilation.Create("temp")
        .AddReferences(...)
        .AddSyntaxTrees(tree);
    var model = compilation.GetSemanticModel(tree);

    // 5. Perform refactoring logic
    // ...

    // 6. Return result
    return RefactoringResult.Success(...);
}
```

**Recommendation: Create Base Class to Reduce Duplication**

```csharp
public abstract class RefactoringBase
{
    protected RefactoringContext CreateContext(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = (CompilationUnitSyntax)tree.GetRoot();

        var errors = tree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Any())
        {
            var errorMsg = string.Join(", ", errors.Select(e => e.GetMessage()).Take(3));
            return new RefactoringContext
            {
                IsValid = false,
                Error = $"Syntax errors: {errorMsg}"
            };
        }

        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(GetStandardReferences())
            .AddSyntaxTrees(tree);

        var model = compilation.GetSemanticModel(tree);

        return new RefactoringContext
        {
            IsValid = true,
            Tree = tree,
            Root = root,
            Compilation = compilation,
            SemanticModel = model
        };
    }

    protected MetadataReference[] GetStandardReferences()
    {
        return new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        };
    }

    protected abstract RefactoringResult ExecuteCore(RefactoringContext context);

    public RefactoringResult Execute(params...)
    {
        var context = CreateContext(sourceCode);
        if (!context.IsValid)
            return RefactoringResult.Failure(context.Error);

        return ExecuteCore(context);
    }
}

public class RefactoringContext
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public SyntaxTree Tree { get; set; }
    public CompilationUnitSyntax Root { get; set; }
    public CSharpCompilation Compilation { get; set; }
    public SemanticModel SemanticModel { get; set; }
}
```

**Benefits:**
- Eliminates 20-30 lines of boilerplate per refactoring
- Ensures consistent error handling
- Centralized reference management
- Easier to add future enhancements (caching, logging)

**Estimated Effort:** 1 day to refactor, 1 day to test

**Recommendation:** Add to Phase 3 as technical debt cleanup

---

### 3.2 Symbol Resolution Utilities

**Multiple refactorings need symbol finding:**
- Rename: Find symbol at position
- Inline Variable: Find variable symbol
- Inline Method: Find method symbol
- Safe Delete: Find symbol and references

**Recommendation: Create Shared SymbolFinder Utility**

```csharp
public class SymbolResolutionHelper
{
    public static ISymbol? FindSymbolAtPosition(
        SemanticModel model,
        SyntaxTree tree,
        int lineNumber,
        int columnNumber)
    {
        var position = tree.GetText().Lines[lineNumber - 1].Start + columnNumber - 1;
        var token = tree.GetRoot().FindToken(position);
        var node = token.Parent;

        return model.GetSymbolInfo(node).Symbol;
    }

    public static ISymbol? FindSymbolByName(
        SemanticModel model,
        SyntaxNode scope,
        string symbolName,
        SymbolKind kind)
    {
        // Search for symbol by name within scope
        // Disambiguate by kind (Field, Method, Local, Parameter)
    }

    public static List<SyntaxNode> FindReferences(
        ISymbol symbol,
        Compilation compilation)
    {
        // Wrapper for async FindReferencesAsync with sync result
        var references = SymbolFinder.FindReferencesAsync(symbol, compilation.Solution)
            .GetAwaiter().GetResult();

        return references
            .SelectMany(r => r.Locations)
            .Select(loc => loc.Location.SourceTree.GetRoot().FindNode(loc.Location.SourceSpan))
            .ToList();
    }
}
```

**Estimated Effort:** 2 days

**Recommendation:** Implement in Phase 1 as part of Rename refactoring

---

### 3.3 Testing Strategy

**Current State:**
- 114 tests (92 unit + 14 component + 8 integration)
- 86.5% line coverage, 82.8% branch coverage

**Gap:** PRD specifies 90% coverage target but doesn't define how to achieve it

**Recommendations:**

1. **Test Categorization:**
   - Unit tests: Individual refactoring methods (input validation, edge cases)
   - Component tests: Roslyn integration (semantic model, data flow)
   - Integration tests: End-to-end MCP tool invocation
   - Snapshot tests: Expected output for common scenarios

2. **Coverage Requirements by Refactoring:**
   - Each new refactoring: Minimum 10 unit tests
   - Happy path: 2-3 tests (simple, medium, complex)
   - Error cases: 3-4 tests (invalid input, unsupported syntax, conflicts)
   - Edge cases: 3-4 tests (generics, nullable, arrays, nested types)

3. **Test Infrastructure:**
   ```csharp
   public abstract class RefactoringTestBase
   {
       protected void AssertRefactoringSuccess(
           string input,
           string expected,
           Action<Refactoring> configure)
       {
           var refactoring = CreateRefactoring();
           configure(refactoring);

           var result = refactoring.Execute(...);

           Assert.True(result.IsSuccess);
           Assert.Equal(NormalizeWhitespace(expected), NormalizeWhitespace(result.RefactoredCode));
       }

       protected void AssertRefactoringFailure(
           string input,
           string expectedError,
           Action<Refactoring> configure)
       {
           var refactoring = CreateRefactoring();
           configure(refactoring);

           var result = refactoring.Execute(...);

           Assert.False(result.IsSuccess);
           Assert.Contains(expectedError, result.ErrorMessage);
       }
   }
   ```

4. **Snapshot Testing:**
   Use Verify library for complex output verification:
   ```csharp
   [Fact]
   public Task Rename_ComplexClass_GeneratesCorrectOutput()
   {
       var input = LoadTestFile("ComplexClass.cs");
       var result = new RenameSymbol().Execute(input, 10, 5, "newName");

       return Verify(result.RefactoredCode);
   }
   ```

**Estimated Effort:** 1-2 days per refactoring for test development

**Recommendation:** Make test writing part of each phase's Definition of Done

---

### 3.4 Performance Optimization

**Current State:** No performance benchmarks exist

**PRD Target:** 2 seconds for typical files (<1000 LOC)

**Architectural Concerns:**

1. **Compilation Creation Overhead**
   - Every refactoring creates new `CSharpCompilation`
   - Includes metadata references (mscorlib, System.Collections, System.Linq)
   - Loading metadata can take 100-500ms per refactoring

2. **Semantic Model Creation**
   - Semantic analysis is expensive for large files
   - Current approach: Create new semantic model every time

3. **Symbol Finding**
   - `FindReferencesAsync` can be slow for complex solutions
   - Single-file scope helps, but still requires symbol resolution

**Recommendations:**

1. **Add Performance Benchmarks:**
   ```csharp
   [MemoryDiagnoser]
   public class RefactoringBenchmarks
   {
       [Params(100, 500, 1000, 5000)]
       public int LinesOfCode { get; set; }

       [Benchmark]
       public void ExtractMethod_Performance()
       {
           var code = GenerateCodeWithLines(LinesOfCode);
           var refactoring = new ExtractMethod();
           var result = refactoring.Execute(code, 10, 20, "ExtractedMethod");
       }
   }
   ```

2. **Caching Strategy (Future):**
   - Cache metadata references (save 100-200ms per refactoring)
   - Cache semantic models for unchanged files
   - Out of scope for V1, but document for V2

3. **Timeout Implementation:**
   - PRD mentions 5-second timeout but doesn't specify implementation
   - Recommendation:
     ```csharp
     public RefactoringResult Execute(params...)
     {
         using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

         try
         {
             return ExecuteCore(cts.Token);
         }
         catch (OperationCanceledException)
         {
             return RefactoringResult.Failure("Refactoring timed out (5 seconds). Try a smaller selection.");
         }
     }
     ```

4. **Performance Acceptance Criteria:**
   - 100 LOC: < 500ms
   - 500 LOC: < 1 second
   - 1000 LOC: < 2 seconds
   - 5000 LOC: < 5 seconds (timeout)

**Estimated Effort:** 2-3 days for benchmarking framework + initial measurements

**Recommendation:** Add to Phase 3 as validation before release

---

## 4. Architecture Recommendations

### 4.1 Code Organization

**Current Structure:**
```
RefactorCsharpMCP.Core/
├── Analysis/
│   ├── DependencyAnalyzer.cs
│   └── ScopeAnalyzer.cs
├── Refactorings/
│   ├── ExtractMethod.cs
│   ├── ConstructorInjection.cs
│   ├── MakeFieldReadonly.cs
│   ├── SafeDelete.cs
│   └── ExtractClass.cs
└── McpToolConstants.cs
```

**Recommended Structure for V1:**
```
RefactorCsharpMCP.Core/
├── Analysis/
│   ├── DependencyAnalyzer.cs
│   ├── ScopeAnalyzer.cs
│   └── SymbolResolutionHelper.cs       # NEW - Shared symbol utilities
├── Refactorings/
│   ├── Base/
│   │   ├── RefactoringBase.cs          # NEW - Base class for all refactorings
│   │   └── RefactoringContext.cs       # NEW - Shared context object
│   ├── CodeExtraction/
│   │   ├── ExtractMethod.cs
│   │   ├── ExtractClass.cs
│   │   ├── InlineVariable.cs           # NEW
│   │   └── InlineMethod.cs             # NEW
│   ├── DependencyManagement/
│   │   ├── ConstructorInjection.cs
│   │   └── IntroduceParameterObject.cs # NEW
│   ├── CodeCleanup/
│   │   ├── MakeFieldReadonly.cs
│   │   ├── SafeDelete.cs
│   │   ├── RenameSymbol.cs             # NEW
│   │   └── RemoveUnusedUsings.cs       # NEW
│   └── Common/
│       ├── DataFlowAnalyzer.cs         # Extract from ExtractMethod
│       └── NameConflictChecker.cs      # Shared by Rename, Extract
├── Validation/
│   └── CSharpIdentifierValidator.cs    # Extract from McpToolConstants
└── McpToolConstants.cs
```

**Benefits:**
- Clear categorization by refactoring type
- Easier to locate related functionality
- Supports future growth (V2 may have 20+ refactorings)
- Separates infrastructure from domain logic

**Estimated Effort:** 1 day reorganization + 1 day namespace updates

**Recommendation:** Defer to Phase 3 or after V1 release (optional improvement)

---

### 4.2 Error Handling Improvements

**Current Pattern:**
```csharp
catch (Exception ex)
{
    var errorCategory = ex switch
    {
        ArgumentException => "InvalidInput",
        InvalidOperationException => "InvalidState",
        FormatException => "ParseError",
        _ => "UnexpectedError"
    };
    return RefactoringResult.Failure($"An error occurred ({errorCategory})...");
}
```

**Issue:** Loses exception details, making debugging difficult

**Recommended Enhancement:**

```csharp
public class RefactoringResult
{
    public bool IsSuccess { get; init; }
    public string? RefactoredCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public ErrorCode ErrorCode { get; init; }           // NEW
    public Dictionary<string, string>? ErrorDetails { get; init; }  // NEW

    public static RefactoringResult Failure(
        string errorMessage,
        ErrorCode code = ErrorCode.UnexpectedError,
        Dictionary<string, string>? details = null)
    {
        return new RefactoringResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ErrorCode = code,
            ErrorDetails = details,
            Message = $"Refactoring failed: {errorMessage}"
        };
    }
}

public enum ErrorCode
{
    None = 0,

    // Validation errors (1xx)
    InvalidInput = 100,
    InvalidIdentifier = 101,
    NameConflict = 102,

    // Syntax errors (2xx)
    ParseError = 200,
    SyntaxError = 201,

    // Semantic errors (3xx)
    SymbolNotFound = 300,
    TypeMismatch = 301,
    ReferenceNotResolved = 302,

    // Refactoring errors (4xx)
    UnsupportedPattern = 400,
    ComplexityExceeded = 401,
    HasReferences = 402,

    // System errors (5xx)
    UnexpectedError = 500,
    Timeout = 501
}
```

**Benefits:**
- AI clients can handle errors programmatically
- Better telemetry and debugging
- Users get more actionable error messages

**Estimated Effort:** 2 days

**Recommendation:** Add to Phase 2 as infrastructure improvement

---

### 4.3 MCP Tool API Design

**Current API Example (Extract Method):**
```json
{
  "sourceCode": "string",
  "startLine": "number",
  "endLine": "number",
  "newMethodName": "string"
}
```

**Issue with Rename (as noted in section 2.1):**
PRD specifies `symbolName` but this is ambiguous. Need position-based resolution.

**Recommended API Consistency:**

All refactorings should follow these patterns:

1. **Selection-Based Refactorings:** Use line ranges
   ```json
   {
     "sourceCode": "string",
     "startLine": "number",
     "endLine": "number",
     ...
   }
   ```

2. **Symbol-Based Refactorings:** Use position + name (for validation)
   ```json
   {
     "sourceCode": "string",
     "lineNumber": "number",
     "columnNumber": "number",
     "symbolName": "string",  // For validation only
     ...
   }
   ```

3. **File-Level Refactorings:** Just source code
   ```json
   {
     "sourceCode": "string"
   }
   ```

**Specific Tool Signatures:**

```typescript
// Rename Symbol
interface RenameSymbolParams {
  sourceCode: string;
  lineNumber: number;        // 1-based line number
  columnNumber: number;      // 1-based column number
  newName: string;
}

// Inline Variable
interface InlineVariableParams {
  sourceCode: string;
  lineNumber: number;        // Line where variable is declared
  variableName: string;      // For validation
}

// Inline Method
interface InlineMethodParams {
  sourceCode: string;
  className: string;
  methodName: string;
}

// Remove Unused Usings
interface RemoveUnusedUsingsParams {
  sourceCode: string;
}

// Introduce Parameter Object
interface IntroduceParameterObjectParams {
  sourceCode: string;
  className: string;
  methodName: string;
  parameterNames: string[];  // Parameters to group
  newClassName: string;       // Name for parameter object
}
```

**Recommendation:** Document all MCP tool signatures in Appendix B of PRD

---

## 5. Priority & Phasing Feedback

### 5.1 Proposed Timeline Analysis

**PRD Phases:**
- Phase 1: Weeks 1-2 (Rename + Extract Method enhancement)
- Phase 2: Weeks 3-4 (Inline Variable + Inline Method)
- Phase 3: Week 5 (Documentation)
- Phase 4: Week 6 (Parameter Object + Remove Usings)

### 5.2 Architectural Assessment of Timeline

**CONCERN: Phase 1 is underestimated**

**Analysis:**
- Rename: 5-7 days (underestimated)
- Extract Method Enhancement: 3-4 days
- Testing: 3-4 days
- Total: 11-15 days (2.2-3 weeks at 5 days/week)

**Recommendation:** **Extend Phase 1 to 3 weeks**

### 5.3 Revised Implementation Order

Based on technical dependencies and complexity:

**Phase 1: Critical Foundation (Weeks 1-3)**
1. **Shared Infrastructure** (3 days)
   - RefactoringBase abstract class
   - SymbolResolutionHelper utility
   - Enhanced error handling with ErrorCode

2. **Remove Unused Usings** (3 days) ← **MOVE UP** from P2
   - Reason: Easiest refactoring, builds confidence
   - No dependencies on other refactorings
   - Good warm-up for team

3. **Rename Symbol** (7 days)
   - Most complex, requires spike work first
   - Foundation for other refactorings
   - Highest user value

4. **Extract Method Enhancement** (4 days)
   - Builds on existing code
   - Return value detection
   - Tuple support

**Phase 2: Code Manipulation (Weeks 4-6)**
1. **Inline Variable** (5 days)
   - Simpler than Inline Method
   - Standalone feature

2. **Extract Class Enhancement** (4 days) ← **ADD**
   - Auto-update references within same class
   - Reduces manual user work
   - High value/effort ratio

3. **Inline Method** (8 days)
   - Most complex of Phase 2
   - Builds on Inline Variable experience

**Phase 3: Polish & Validation (Week 7)**
1. **Documentation Updates** (2 days)
2. **Performance Benchmarking** (2 days)
3. **Test Coverage Validation** (1 day)

**Phase 4: Optional Enhancements (Week 8)**
1. **Introduce Parameter Object** (6 days)
2. **Code Organization Refactoring** (2 days)

**Total Timeline: 7-8 weeks** (vs original 6 weeks)

### 5.4 Dependency Graph

```
Phase 1:
  Shared Infrastructure
    ↓
  Remove Unused Usings (no dependencies)
    ↓
  Rename Symbol ← requires SymbolResolutionHelper
    ↓
  Extract Method Enhancement ← builds on existing

Phase 2:
  Inline Variable ← uses SymbolResolutionHelper
    ↓
  Extract Class Enhancement ← builds on existing
    ↓
  Inline Method ← harder version of Inline Variable

Phase 3:
  Documentation + Performance + Testing (all Phase 1-2 complete)

Phase 4:
  Introduce Parameter Object (optional)
```

### 5.5 Risk Mitigation in Revised Plan

1. **Early Win Strategy:** Remove Unused Usings in Phase 1 provides quick success
2. **Parallel Work Opportunities:** Documentation can start during Phase 2
3. **Flexible Scope:** Phase 4 is optional, can be V1.1 if timeline slips
4. **De-risked Rename:** Spike work before full implementation

---

## 6. Open Questions & Concerns

### 6.1 Technical Questions Requiring Resolution

1. **Question: Async API Integration Strategy**
   - Problem: `SymbolFinder.FindReferencesAsync()` is async, but all refactorings are sync
   - Options:
     - A) Make all refactorings async (breaking change)
     - B) Use `.GetAwaiter().GetResult()` (blocking)
     - C) Implement custom synchronous symbol finding
   - **Recommendation:** Option B for V1 (single-file scope makes this acceptable)
   - **Action:** Document decision in architecture docs

2. **Question: How to Handle Partial Classes?**
   - Current: All refactorings assume single-file, single class definition
   - Partial classes span multiple files
   - **Recommendation:** Explicitly reject partial classes in V1
   - **Action:** Add validation to detect `partial` keyword and fail gracefully

3. **Question: C# Language Version Targeting**
   - Tuple syntax requires C# 7.0+
   - Nullable reference types are C# 8.0+
   - Target frameworks may use older language versions
   - **Recommendation:** Generate code compatible with C# 7.3 (last version before 8.0)
   - **Action:** Avoid NRT syntax in generated code, use tuple syntax cautiously

4. **Question: Should Rename Support Type Renaming in V1?**
   - PRD says "No" (line 827-829)
   - Architectural perspective: **Agree - defer to V1.1**
   - Reason: Type renaming requires cross-file analysis for correctness
   - **Action:** Document as explicit limitation

5. **Question: Extract Method Return Value - Single or Multiple?**
   - PRD mentions tuple support but unclear if V1
   - **Recommendation:** Support both single return and tuple returns in Phase 1
   - Reason: Tuple syntax is stable in C# 7.0+, not significantly more complex
   - **Action:** Clarify in PRD Phase 1 deliverables

### 6.2 Testing & Quality Concerns

1. **Concern: No Integration Test Strategy for New Refactorings**
   - Current: 8 integration tests for existing refactorings
   - Need: Integration tests for all 6 new refactorings
   - **Recommendation:** Minimum 2 integration tests per new refactoring (12 total)
   - **Action:** Add to Definition of Done for each phase

2. **Concern: Snapshot Testing Not Mentioned**
   - Complex refactorings benefit from snapshot tests
   - **Recommendation:** Use Verify library for output verification
   - **Action:** Add snapshot testing to Phase 3

3. **Concern: No Performance Regression Testing**
   - As code grows, refactorings may slow down
   - **Recommendation:** Add BenchmarkDotNet baseline measurements
   - **Action:** Create benchmarks in Phase 3, run in CI

### 6.3 Product & Documentation Concerns

1. **Concern: User Expectations vs Reality Gap**
   - Users from Visual Studio expect cross-file refactoring
   - Single-file limitation may frustrate users
   - **Recommendation:** Prominent warning in ALL documentation
   - **Action:** Add banner to README: "IMPORTANT: V1 refactorings are single-file only"

2. **Concern: Extract Class Manual Reference Updates**
   - Requiring manual updates is error-prone
   - Users may forget to update references → broken code
   - **Recommendation:** Add semi-automatic reference updating in Phase 2
   - **Action:** Change PRD to include basic reference updates in Phase 2

3. **Concern: No Migration Guide for Existing Users**
   - 4 refactorings changing (enhancements, new parameters)
   - **Recommendation:** Create migration guide for V1
   - **Action:** Document breaking changes and new capabilities

### 6.4 Architecture & Technical Debt

1. **Concern: Compilation Creation Overhead**
   - Every refactoring creates new compilation + semantic model
   - May impact performance for large files
   - **Recommendation:** Measure in Phase 3, optimize in V1.1 if needed
   - **Action:** Add to performance benchmarking tasks

2. **Concern: No Centralized Validation**
   - Each refactoring validates identifiers independently
   - Duplication across 10+ refactorings
   - **Recommendation:** Create `CSharpIdentifierValidator` utility class
   - **Action:** Add to Phase 1 infrastructure work

3. **Concern: Error Messages Not User-Friendly Enough**
   - Current: "An error occurred (InvalidState)"
   - Better: "Cannot extract method: selection contains early return statement (not supported in V1)"
   - **Recommendation:** Improve error messages with context
   - **Action:** Add error message guidelines to development docs

---

## 7. Recommendations Summary

### 7.1 Critical Recommendations (Must Address)

1. ✅ **Extend Phase 1 timeline from 2 weeks to 3 weeks**
   - Rename is more complex than estimated
   - Shared infrastructure needs time to build properly

2. ✅ **Add spike task for Rename refactoring (2 days before implementation)**
   - Prototype symbol resolution
   - Validate async API integration strategy
   - Identify edge cases early

3. ✅ **Move Remove Unused Usings to Phase 1**
   - Easiest refactoring provides early win
   - Good morale boost for team

4. ✅ **Add Extract Class reference updating to Phase 2**
   - Reduces user manual work significantly
   - Prevents broken code scenarios

5. ✅ **Update MCP tool signatures to include position parameters for Rename**
   - Change from `symbolName` to `lineNumber + columnNumber + symbolName`
   - Required for accurate symbol resolution

### 7.2 High-Value Recommendations (Should Address)

1. ⭐ **Create RefactoringBase abstract class**
   - Eliminates boilerplate across all refactorings
   - Ensures consistent error handling

2. ⭐ **Build SymbolResolutionHelper utility**
   - Shared by Rename, Inline Variable, Inline Method, Safe Delete
   - Centralizes complex symbol finding logic

3. ⭐ **Enhance error handling with ErrorCode enum**
   - Enables programmatic error handling by AI clients
   - Better telemetry and debugging

4. ⭐ **Add performance benchmarking framework in Phase 3**
   - Validates 2-second performance target
   - Prevents performance regressions

5. ⭐ **Create comprehensive integration test suite**
   - Minimum 2 integration tests per refactoring
   - End-to-end MCP tool validation

### 7.3 Optional Recommendations (Nice to Have)

1. 💡 **Reorganize code into categorized folders**
   - CodeExtraction/, DependencyManagement/, CodeCleanup/
   - Defer to post-V1 or Phase 4

2. 💡 **Add snapshot testing with Verify library**
   - Easier output validation for complex refactorings
   - Reduces test maintenance burden

3. 💡 **Consider moving Inline Method to V1.1**
   - Complexity is higher than P1 suggests
   - Could slip timeline if included

---

## 8. Conclusion

The PRD for V1 Refactoring Capabilities is **well-researched, pragmatically scoped, and architecturally sound**. The product owner has made excellent prioritization decisions based on industry research and user needs.

### Key Takeaways

**Strengths:**
- Clear scope boundaries (IN SCOPE vs OUT OF SCOPE)
- Realistic phasing aligned with user value
- Strong foundation in existing codebase
- Appropriate use of single-file limitation for V1

**Areas for Improvement:**
- Rename complexity underestimated (5-7 days, not 3-4)
- Extract Method return detection not trivial
- Timeline needs 1-2 week extension
- Shared infrastructure should be built upfront

### Final Recommendation

**APPROVE PRD with modifications:**

1. **Accept all 10 proposed refactorings** - technically feasible with Roslyn
2. **Adjust Phase 1 timeline** - 2 weeks → 3 weeks
3. **Reorder implementation** - Remove Unused Usings first, then Rename
4. **Add infrastructure work** - RefactoringBase, SymbolResolutionHelper
5. **Enhance Extract Class** - Add basic reference updating in Phase 2
6. **Total timeline:** 7-8 weeks (vs original 6 weeks)

### Next Steps

1. Product owner updates PRD with revised timeline
2. Create GitHub issues for Phase 1 work:
   - Issue #1: Build shared refactoring infrastructure
   - Issue #2: Implement Remove Unused Usings
   - Issue #3: Spike - Rename symbol resolution (2 days)
   - Issue #4: Implement Rename Symbol
   - Issue #5: Enhance Extract Method with return value detection
3. Architect creates Software Design Document (SDD) for complex refactorings
4. Begin Phase 1 implementation

---

**Reviewed by:** Master Software Architect
**Date:** 2025-10-09
**Status:** Approved with Recommendations
**Next Review:** After Phase 1 completion (Week 3)

