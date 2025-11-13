# Bug #2 Verification: Call Site Update Analysis

**Test Date**: 2025-11-13
**Tool Version**: Post-PR #113, Post-PR #117
**Test Method**: extract_class with validateCompilation: false
**Issue**: #112 - Call site updates not working

## Test Setup

**Source**: ExtractMethod.cs from commit 7862d80 (457 lines, pre-MethodGenerator extraction)
**Operation**: Extract 5 methods into MethodGenerator class
**Methods Extracted**:
- BuildExtractedMethod
- BuildMethodCall
- ReplaceStatementsWithMethodCall
- GenerateReturnType
- GenerateReturnStatement

**Expected Behavior**: Tool should update 3 call sites to use `_methodGenerator.MethodName(...)`

## Results

### ✅ What Worked

1. **Composition Field Created**:
   ```csharp
   private readonly MethodGenerator _methodGenerator = new MethodGenerator();
   ```

2. **New Class Created**: MethodGenerator class with all 5 methods extracted correctly

3. **Method Visibility**: All methods correctly made `internal` for new class

4. **Tool Message**: "All references within the same class have been automatically updated."

### ❌ What Failed - Bug #2 STILL EXISTS

**Call Site 1** (Line ~163):
```csharp
// EXPECTED:
var extractedMethod = _methodGenerator.BuildExtractedMethod(newMethodName, statementsToExtract, dataFlowAnalysis, containingMethod, targetFramework);

// ACTUAL (NOT UPDATED):
var extractedMethod = BuildExtractedMethod(newMethodName, statementsToExtract, dataFlowAnalysis, containingMethod, targetFramework);
```

**Call Site 2** (Line ~172):
```csharp
// EXPECTED:
var methodCall = _methodGenerator.BuildMethodCall(newMethodName, dataFlowAnalysis.Parameters, dataFlowAnalysis.ReturnInfo);

// ACTUAL (NOT UPDATED):
var methodCall = BuildMethodCall(newMethodName, dataFlowAnalysis.Parameters, dataFlowAnalysis.ReturnInfo);
```

**Call Site 3** (Line ~182):
```csharp
// EXPECTED:
var updatedMethod = _methodGenerator.ReplaceStatementsWithMethodCall(containingMethod, statementsToExtract, methodCall);

// ACTUAL (NOT UPDATED):
var updatedMethod = ReplaceStatementsWithMethodCall(containingMethod, statementsToExtract, methodCall);
```

## Analysis

### Pattern Not Handled

The tool fails to update method invocations that follow this pattern:
```csharp
var result = MethodName(arguments);
```

This is a **variable assignment with method invocation** pattern. The tool appears to only handle:
- Direct invocations: `MethodName(args);`
- Property/field access patterns

### Comparison with Previous Test

| Metric | Initial Test (cddff7b) | Re-test (Latest Tool) |
|--------|------------------------|----------------------|
| Composition field created | ✅ Yes | ✅ Yes |
| Methods extracted | ✅ 5/5 | ✅ 5/5 |
| Call sites updated | ❌ 0/3 | ❌ 0/3 |
| Tool success message | ✅ Claims updated | ✅ Claims updated |

**Conclusion**: Bug #2 regression persists. No improvement between PR #113 and current version.

## Root Cause Hypothesis

The ReferenceUpdater component in ExtractClass likely:
1. Searches for `SimpleMemberAccessExpression` nodes (e.g., `this.MethodName`)
2. Does NOT search for simple `IdentifierName` nodes in invocation contexts
3. Misses variable assignment patterns: `var x = MethodName();`

## Recommendations

### Immediate Action
1. Document this finding in Issue #112 (update existing comment)
2. Manual delegation fixes still required for current work
3. Skip tool-assisted extractions until Bug #2 is fixed

### For Tool Fix
ReferenceUpdater needs to handle:
```csharp
// Pattern 1: Variable assignment invocation
var result = MethodName(args);

// Pattern 2: Direct invocation
MethodName(args);

// Pattern 3: Return statement invocation
return MethodName(args);

// Pattern 4: Expression invocation
if (MethodName(args) == value) { }

// Pattern 5: Chained invocation
OtherMethod(MethodName(args));
```

All these patterns need the same transformation:
```csharp
MethodName(...) → _field.MethodName(...)
```

## Impact on Issue #91

For remaining extractions on refactor/91 branch:
- ✅ Use tool for method extraction (creates clean classes)
- ⚠️ Expect manual delegation fixes for ALL call sites
- 📝 Track time spent on manual fixes vs tool use
- 🎯 Goal: Provide data for Bug #2 priority assessment
