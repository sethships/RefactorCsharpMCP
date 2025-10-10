# Product Requirements Document: RefactorCsharpMCP V1 Refactoring Capabilities

**Version:** 1.0.0
**Date:** 2025-10-09
**Status:** Final - Ready for Implementation
**Author:** Product Owner (Master)

---

## Executive Summary

RefactorCsharpMCP V1 aims to deliver a focused, robust set of refactoring capabilities that address the most common C# code quality needs for AI-assisted development workflows. Rather than attempting comprehensive coverage of all possible refactorings, V1 prioritizes **breadth of common use cases** over **depth of edge cases**, ensuring each supported refactoring works reliably for typical development scenarios.

The V1 release will support **8 core refactoring operations** across three categories: Code Extraction & Composition, Dependency Management, and Code Cleanup. These refactorings represent the operations developers perform most frequently and deliver the highest impact on code maintainability.

---

## Goals & Objectives

### Primary Goals
1. **Enable AI-Assisted Refactoring**: Provide Claude Code and other MCP clients with reliable, production-ready C# refactoring capabilities
2. **Focus on Common Cases**: Support the 80% use case for each refactoring, deferring edge cases to future versions
3. **Ensure Correctness**: Every refactoring must preserve code semantics and compile successfully
4. **Maintain Simplicity**: Keep the API surface small and intuitive for AI clients to use effectively

### Success Criteria
- ✅ All 8 refactorings work correctly for common cases (≥90% test coverage)
- ✅ No breaking changes to existing refactorings
- ✅ Clear documentation of limitations and out-of-scope scenarios
- ✅ Performance: All refactorings complete within 2 seconds for typical files (<1000 LOC)
- ✅ Integration: Seamless MCP tool invocation from Claude Code and other clients

---

## Target Users

### Primary Users
- **AI-Assisted Developers**: Developers using Claude Code, Cursor, VS Code with MCP extensions to refactor C# code
- **Individual Contributors**: Solo developers working on .NET projects who want quick, reliable refactorings

### Secondary Users
- **Development Teams**: Teams adopting AI-assisted development practices for C# codebases
- **Code Reviewers**: Developers who request refactorings during pull request reviews

### User Personas

**Persona 1: Sarah - Full-Stack Developer**
- Uses Claude Code daily for C# backend development
- Frequently extracts methods to reduce code duplication
- Needs dependency injection patterns for testability
- Values speed and reliability over comprehensive features

**Persona 2: Mike - Legacy Code Maintainer**
- Works with existing .NET Framework 4.5.2+ codebases
- Regularly cleans up code smells (long methods, god classes)
- Needs safe refactorings that don't break existing functionality
- Prefers explicit, predictable transformations

---

## V1 Refactoring Catalog

### Category 1: Code Extraction & Composition

#### 1.1 Extract Method ✅ **[IMPLEMENTED - ENHANCE]**

**Description**: Extract a block of consecutive statements into a new private method with automatic parameter detection and proper scoping.

**Priority**: **P0** - Most frequently used refactoring across all languages

**Use Cases**:
- Reducing method complexity (breaking down long methods)
- Eliminating code duplication
- Improving code readability with well-named extracted methods
- Preparing code for reuse

**V1 Scope (IN SCOPE)**:
- ✅ Extract consecutive statements from a single method
- ✅ Automatic detection of input parameters (data flowing into selection)
- ✅ Preserve static/instance context (extracted method matches containing method)
- ✅ Support for local variables, method parameters, and primitive types
- ✅ Generic types (Dictionary<K,V>, List<T>, etc.)
- ✅ Nullable reference types (string?, List<int>?)
- ✅ Array types (int[], string[][])
- ✅ Instance field access (methods can access class fields directly)
- ✅ Single file refactoring only

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Return value detection (V1 only supports void methods)
- ❌ Extraction across multiple methods
- ❌ Async/await method extraction (detected but not auto-generated)
- ❌ LINQ expression extraction with proper closure handling
- ❌ Out/ref parameter handling
- ❌ Extract to different visibility levels (always private in V1)
- ❌ Cross-file extraction
- ❌ Partial class extraction

**Example**:

**Before:**
```csharp
public void ProcessOrder(Order order)
{
    // Validation
    if (order == null) throw new ArgumentNullException(nameof(order));
    if (order.Total <= 0) throw new ArgumentException("Invalid total");

    // Processing
    var discount = CalculateDiscount(order);
    var tax = CalculateTax(order.Total - discount);
    var final = order.Total - discount + tax;

    SaveOrder(order, final);
}
```

**After (lines 5-8 extracted as "CalculateFinalAmount"):**
```csharp
public void ProcessOrder(Order order)
{
    // Validation
    if (order == null) throw new ArgumentNullException(nameof(order));
    if (order.Total <= 0) throw new ArgumentException("Invalid total");

    var final = CalculateFinalAmount(order);

    SaveOrder(order, final);
}

private decimal CalculateFinalAmount(Order order)
{
    var discount = CalculateDiscount(order);
    var tax = CalculateTax(order.Total - discount);
    var final = order.Total - discount + tax;
    return final;
}
```

**Success Criteria**:
- Extracted method compiles without errors
- Original method behavior unchanged (no side effects)
- Parameters correctly identified via data flow analysis
- Proper indentation and formatting

**Current Implementation Status**: ✅ Implemented, needs enhancement for return value detection

---

#### 1.2 Extract Class ✅ **[IMPLEMENTED - DOCUMENT LIMITATIONS]**

**Description**: Extract selected fields and methods from a class into a new class with composition pattern (field holding instance of new class).

**Priority**: **P1** - Common for refactoring god classes and improving cohesion

**Use Cases**:
- Breaking down god classes into focused, cohesive classes
- Separating concerns (e.g., extract logging, validation, or data access logic)
- Creating value objects from primitive fields

**V1 Scope (IN SCOPE)**:
- ✅ Extract multiple fields into a new class
- ✅ Extract methods that operate on those fields
- ✅ Create composition relationship (original class holds instance of new class)
- ✅ Generate readonly field for new class instance
- ✅ Single file refactoring only

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Automatic reference updates (V1 requires manual fixes - documented limitation)
- ❌ Extract to separate file
- ❌ Extract with delegation pattern (auto-generate delegate methods)
- ❌ Interface extraction
- ❌ Inheritance-based extraction
- ❌ Constructor parameter injection for extracted class

**Example**:

**Before:**
```csharp
public class Customer
{
    private string _street;
    private string _city;
    private string _zipCode;
    private string _name;
    private string _email;

    public string GetFullAddress()
    {
        return $"{_street}, {_city}, {_zipCode}";
    }

    public void SendEmail(string message) { /* ... */ }
}
```

**After (extract _street, _city, _zipCode, GetFullAddress as "Address"):**
```csharp
public class Customer
{
    private readonly Address _address = new Address();
    private string _name;
    private string _email;

    public void SendEmail(string message) { /* ... */ }
}

public class Address
{
    private string _street;
    private string _city;
    private string _zipCode;

    public string GetFullAddress()
    {
        return $"{_street}, {_city}, {_zipCode}";
    }
}
```

**⚠️ IMPORTANT**: V1 does NOT automatically update references. Developer must manually change calls from `GetFullAddress()` to `_address.GetFullAddress()`.

**Success Criteria**:
- New class created with extracted members
- Original class removes extracted members and adds new class field
- Code compiles (after manual reference updates)
- Warning message displayed about required manual updates

**Current Implementation Status**: ✅ Implemented with documented manual update requirement

---

#### 1.3 Inline Method

**Description**: Replace all calls to a simple method with the method's body, removing the method definition.

**Priority**: **P1** - Common for removing unnecessary abstractions

**Use Cases**:
- Removing over-abstracted single-use methods
- Simplifying code where method name doesn't add clarity
- Cleaning up after Extract Method when extraction wasn't beneficial

**V1 Scope (IN SCOPE)**:
- ✅ Inline simple methods (no parameters or return values)
- ✅ Inline methods with simple parameters (primitives, strings)
- ✅ Single caller detection and replacement
- ✅ Multiple callers with consistent context
- ✅ Preserve comments from inlined method
- ✅ Single file refactoring only

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Methods with out/ref parameters
- ❌ Methods with complex control flow (multiple returns, loops)
- ❌ Recursive method inlining
- ❌ Virtual/override method inlining
- ❌ Cross-file inlining

**Example**:

**Before:**
```csharp
public class Calculator
{
    public int Add(int a, int b)
    {
        return Sum(a, b);
    }

    private int Sum(int a, int b)
    {
        return a + b;
    }
}
```

**After (inline Sum):**
```csharp
public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}
```

**Success Criteria**:
- All calls replaced with method body
- Method definition removed
- Code semantics preserved
- No compilation errors

**Current Implementation Status**: ❌ Not implemented - NEW for V1

---

#### 1.4 Inline Variable

**Description**: Replace all uses of a variable with its initialization expression, removing the variable declaration.

**Priority**: **P1** - Very common for simplifying code with single-use variables

**Use Cases**:
- Removing unnecessary intermediate variables
- Simplifying code where variable name doesn't add clarity
- Cleaning up after refactoring

**V1 Scope (IN SCOPE)**:
- ✅ Inline local variables assigned once and used once or multiple times
- ✅ Simple expressions (literals, method calls, object creation)
- ✅ Preserve expression semantics
- ✅ Single file refactoring only

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Variables modified after initialization (assignment, ++, --)
- ❌ Variables captured in lambdas/closures
- ❌ Variables with complex side effects

**Example**:

**Before:**
```csharp
public void ProcessData()
{
    var threshold = 100;
    if (value > threshold)
    {
        Process();
    }
}
```

**After (inline threshold):**
```csharp
public void ProcessData()
{
    if (value > 100)
    {
        Process();
    }
}
```

**Success Criteria**:
- All variable uses replaced with initialization expression
- Variable declaration removed
- Code semantics preserved
- Parentheses added if needed to preserve precedence

**Current Implementation Status**: ❌ Not implemented - NEW for V1

---

### Category 2: Dependency Management

#### 2.1 Constructor Injection ✅ **[IMPLEMENTED - STABLE]**

**Description**: Convert method parameters to constructor-injected fields (or properties), supporting dependency injection patterns.

**Priority**: **P0** - Essential for testability and modern C# architecture

**Use Cases**:
- Converting static dependencies to injected dependencies
- Preparing classes for dependency injection frameworks
- Improving testability by allowing mock injection

**V1 Scope (IN SCOPE)**:
- ✅ Convert selected method parameters to private readonly fields
- ✅ Convert selected method parameters to public readonly properties
- ✅ Merge with existing constructor (add new parameters)
- ✅ Create new constructor if none exists
- ✅ Update method body to use fields/properties
- ✅ Remove injected parameters from method signature
- ✅ Single file refactoring only

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Multi-constructor handling (V1 only supports single constructor)
- ❌ Constructor chaining
- ❌ Primary constructor conversion (C# 12 feature)
- ❌ Automatic interface extraction

**Example**: See EXAMPLES.md for comprehensive examples

**Success Criteria**:
- Parameters converted to fields/properties
- Constructor created/updated with new parameters
- Method signature updated (parameters removed)
- Method body updated to use fields/properties
- Code compiles successfully

**Current Implementation Status**: ✅ Implemented and stable

---

#### 2.2 Introduce Parameter Object

**Description**: Replace a group of parameters that naturally belong together with a single parameter object.

**Priority**: **P2** - Useful but less frequent than core refactorings

**Use Cases**:
- Reducing method parameter count (addressing "long parameter list" code smell)
- Creating value objects
- Improving API clarity

**V1 Scope (IN SCOPE)**:
- ✅ Create new class/struct for selected parameters
- ✅ Generate readonly properties for each parameter
- ✅ Update method signature to accept parameter object
- ✅ Update method body to use parameter object properties
- ✅ Update all callers to pass parameter object
- ✅ Single file refactoring only

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Record type generation (C# 9+)
- ❌ Cross-file parameter object extraction
- ❌ Optional/default parameter handling
- ❌ Builder pattern generation

**Example**:

**Before:**
```csharp
public void CreateCustomer(string name, string email, string street, string city, string zip)
{
    // ...
}
```

**After (introduce AddressInfo for street, city, zip):**
```csharp
public class AddressInfo
{
    public string Street { get; }
    public string City { get; }
    public string Zip { get; }

    public AddressInfo(string street, string city, string zip)
    {
        Street = street;
        City = city;
        Zip = zip;
    }
}

public void CreateCustomer(string name, string email, AddressInfo address)
{
    // Use address.Street, address.City, address.Zip
}
```

**Success Criteria**:
- New parameter object class created
- Method signature updated
- Method body references updated
- All callers updated to construct parameter object

**Current Implementation Status**: ❌ Not implemented - NEW for V1

---

### Category 3: Code Cleanup

#### 2.3 Make Field Readonly ✅ **[IMPLEMENTED - STABLE]**

**Description**: Add readonly modifier to fields that are only assigned in constructors or at declaration.

**Priority**: **P1** - Common for improving immutability and preventing bugs

**Use Cases**:
- Enforcing immutability
- Preventing accidental field modification
- Improving code safety

**V1 Scope (IN SCOPE)**:
- ✅ Detect fields assigned only in constructors
- ✅ Detect fields with initializers
- ✅ Add readonly modifier in correct position
- ✅ Validate no assignments outside constructors
- ✅ Single file analysis only

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Cross-file assignment detection
- ❌ Batch readonly conversion (V1 processes one field at a time)
- ❌ Lambda/closure assignment analysis (V1 rejects these conservatively)

**Success Criteria**: See current implementation documentation

**Current Implementation Status**: ✅ Implemented and stable

---

#### 2.4 Safe Delete ✅ **[IMPLEMENTED - DOCUMENT LIMITATIONS]**

**Description**: Delete methods or fields after verifying they have no references within the same file.

**Priority**: **P1** - Common for removing unused code

**Use Cases**:
- Removing dead code
- Cleaning up after refactoring
- Removing deprecated methods

**V1 Scope (IN SCOPE)**:
- ✅ Delete methods with no callers (single file)
- ✅ Delete fields with no references (single file)
- ✅ Detect direct references and member access
- ✅ Safety checks before deletion

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Cross-file reference detection (MAJOR LIMITATION)
- ❌ Reflection usage detection
- ❌ Delete with cascade (delete dependent code)
- ❌ Delete interfaces/base classes

**Example**: See current implementation documentation

**⚠️ CRITICAL LIMITATION**: V1 only analyzes references within the same source file. Cross-file references are NOT detected. Use with caution in multi-file projects.

**Success Criteria**:
- No references found within file → deletion succeeds
- References found → error with reference locations
- Warning message about cross-file limitation

**Current Implementation Status**: ✅ Implemented with documented single-file limitation

---

#### 2.5 Rename

**Description**: Rename a symbol (variable, method, class, field, property) and update all references consistently.

**Priority**: **P0** - THE most frequently used refactoring operation (research shows higher frequency than Extract Method)

**Use Cases**:
- Improving code clarity with better names
- Following naming conventions
- Fixing typos in identifiers

**V1 Scope (IN SCOPE)**:
- ✅ Rename local variables within a method
- ✅ Rename method parameters
- ✅ Rename private methods (single file)
- ✅ Rename private fields
- ✅ Update all references within the same file
- ✅ Preserve case sensitivity
- ✅ Validation: new name is valid C# identifier
- ✅ Validation: new name doesn't conflict with existing symbols

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Cross-file renaming
- ❌ Public API renaming (requires cross-file analysis)
- ❌ Namespace renaming
- ❌ Type renaming (classes, interfaces, structs)
- ❌ Rename with preview/diff

**Example**:

**Before:**
```csharp
public class DataProcessor
{
    private int _cnt;

    public void Process()
    {
        var tmp = GetData();
        _cnt = tmp.Length;
    }
}
```

**After (rename _cnt to _itemCount, tmp to data):**
```csharp
public class DataProcessor
{
    private int _itemCount;

    public void Process()
    {
        var data = GetData();
        _itemCount = data.Length;
    }
}
```

**Success Criteria**:
- All references updated to new name
- No compilation errors
- No symbol conflicts
- Semantic equivalence preserved

**Current Implementation Status**: ❌ Not implemented - NEW for V1 (HIGHEST PRIORITY)

---

#### 2.6 Remove Unused Usings

**Description**: Remove using directives that are not referenced in the file.

**Priority**: **P2** - Nice to have, improves code cleanliness

**Use Cases**:
- Cleaning up after refactoring
- Reducing namespace pollution
- Improving compile times (marginally)

**V1 Scope (IN SCOPE)**:
- ✅ Detect unused using directives
- ✅ Remove unused usings
- ✅ Preserve global usings (C# 10+)
- ✅ Single file analysis

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Add missing usings
- ❌ Sort/organize usings
- ❌ Namespace optimization

**Example**:

**Before:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class SimpleClass
{
    public void DoWork()
    {
        Console.WriteLine("Hello");
    }
}
```

**After:**
```csharp
using System;

public class SimpleClass
{
    public void DoWork()
    {
        Console.WriteLine("Hello");
    }
}
```

**Success Criteria**:
- Only used namespaces remain
- Code compiles successfully
- No functionality changes

**Current Implementation Status**: ❌ Not implemented - NEW for V1

---

## V1 Refactoring Summary Table

| # | Refactoring | Priority | Status | Category |
|---|-------------|----------|--------|----------|
| 1 | **Rename** | P0 🔥 | ❌ NEW | Code Cleanup |
| 2 | Extract Method | P0 🔥 | ✅ ENHANCE | Code Extraction |
| 3 | Constructor Injection | P0 🔥 | ✅ STABLE | Dependency Mgmt |
| 4 | Inline Variable | P1 | ❌ NEW | Code Extraction |
| 5 | Inline Method | P1 | ❌ NEW | Code Extraction |
| 6 | Make Field Readonly | P1 | ✅ STABLE | Code Cleanup |
| 7 | Safe Delete | P1 | ✅ DOCUMENT | Code Cleanup |
| 8 | Extract Class | P1 | ✅ DOCUMENT | Code Extraction |
| 9 | Introduce Parameter Object | P2 | ❌ NEW | Dependency Mgmt |
| 10 | Remove Unused Usings | P2 | ❌ NEW | Code Cleanup |

**Total**: 10 refactorings (4 implemented, 6 new)

---

## Implementation Priority & Phasing

### Phase 1: Critical Path (Weeks 1-2)
**Goal**: Implement missing P0 refactorings

1. **Rename** (P0) - Highest priority, most frequently used
2. **Extract Method - Return Value Detection** (P0 enhancement) - Complete existing implementation

**Deliverables**:
- Rename refactoring with single-file scope
- Extract Method with automatic return type detection
- Comprehensive unit tests (>90% coverage)
- Updated EXAMPLES.md

### Phase 2: High-Value Additions (Weeks 3-4)
**Goal**: Add P1 refactorings for complete common case coverage

3. **Inline Variable** (P1)
4. **Inline Method** (P1)

**Deliverables**:
- Working implementations for both refactorings
- Integration tests with real code samples
- Updated documentation

### Phase 3: Polish & Documentation (Week 5)
**Goal**: Document limitations and prepare for release

5. Document **Extract Class** limitations (manual reference updates required)
6. Document **Safe Delete** limitations (single-file analysis only)
7. Update all documentation with scope/limitations
8. Create comprehensive examples for all 8 refactorings

**Deliverables**:
- Updated README.md with limitation warnings
- TROUBLESHOOTING.md updates
- Complete EXAMPLES.md

### Phase 4: Nice-to-Have (Week 6 - Optional)
**Goal**: Add P2 refactorings if time permits

9. **Introduce Parameter Object** (P2)
10. **Remove Unused Usings** (P2)

**Deliverables**: Optional additions, can be deferred to V1.1

---

## Success Metrics

### Functional Metrics
- ✅ All P0 refactorings implemented and tested (3/3)
- ✅ All P1 refactorings implemented and tested (5/5)
- ✅ Test coverage ≥90% for core refactoring logic
- ✅ Zero compilation errors for all refactored code
- ✅ 100% semantic preservation (behavior unchanged)

### Quality Metrics
- ✅ Performance: <2 seconds for files <1000 LOC
- ✅ Memory: <100MB baseline, <500MB peak
- ✅ Reliability: <1% failure rate in production use
- ✅ Documentation: All limitations clearly documented

### User Experience Metrics
- ✅ Clear error messages (no stack traces exposed)
- ✅ Success messages indicate what changed
- ✅ Warning messages for manual steps required
- ✅ MCP tool descriptions are clear and actionable

---

## Out of Scope for V1

### Explicitly Deferred to Future Versions

1. **Cross-File Refactorings**
   - Requires workspace/project analysis
   - Significant complexity increase
   - Target: V2.0

2. **Advanced Type System Support**
   - Pattern matching
   - Discriminated unions
   - Advanced generics (constraints, variance)
   - Target: V1.5

3. **Async/Await Auto-Generation**
   - Complex control flow analysis
   - Requires return type inference
   - Target: V1.5

4. **LINQ Expression Refactoring**
   - Closure handling
   - Expression tree analysis
   - Target: V2.0

5. **Framework-Specific Refactorings**
   - ASP.NET Core patterns
   - Entity Framework optimizations
   - Target: V3.0

6. **AI-Suggested Refactorings**
   - Code smell detection
   - Automatic refactoring recommendations
   - Target: V2.5

---

## Risk Assessment & Mitigation

### Technical Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Roslyn data flow analysis fails for complex code | High | Medium | Conservative fallback: require manual parameters |
| Performance degradation on large files | Medium | Low | Implement timeout (5s) and size limit (5000 LOC) |
| Edge cases cause compilation errors | High | Medium | Comprehensive test suite, validate output compiles |

### Product Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Users expect cross-file refactoring (not supported) | Medium | High | **CLEARLY DOCUMENT** single-file limitation in all docs |
| Extract Class requires manual updates (confusing) | Medium | High | **WARNING MESSAGE** on every extraction |
| Safe Delete misses cross-file references | High | Medium | **WARNING MESSAGE** + recommendation to search |

---

## Assumptions & Dependencies

### Assumptions
1. Target users are comfortable with single-file refactoring scope
2. AI clients (Claude Code, etc.) can guide users on which refactoring to apply
3. Users will read warning messages about limitations
4. .NET 8 runtime is acceptable for all users (server runs on .NET 8, can analyze any C# version)

### Dependencies
- Microsoft.CodeAnalysis.CSharp 4.14.0+ (Roslyn)
- ModelContextProtocol SDK 0.4.0+
- .NET 8 runtime

### External Factors
- Roslyn API stability (low risk - mature API)
- MCP protocol changes (low risk - stable specification)

---

## Open Questions

1. **Should Rename support type (class/interface) renaming in V1?**
   - **Recommendation**: No - requires cross-file analysis for public types
   - **Decision**: Defer to V1.1, focus on local/private symbols only

2. **Should Extract Method support multiple return values (tuples)?**
   - **Recommendation**: Yes if simple, use tuple return types
   - **Decision**: Include in Phase 1 enhancement

3. **Should we add "Extract Interface" to V1?**
   - **Recommendation**: No - P2 priority, complex implementation
   - **Decision**: Defer to V1.5

4. **Performance target: 2 seconds or 5 seconds?**
   - **Recommendation**: 2 seconds for typical files (<500 LOC), 5 seconds max timeout
   - **Decision**: Implement 5s timeout, optimize for 2s average

---

## Appendix A: Research Summary

### Industry Research Findings

1. **Most Frequently Used Refactorings** (based on academic research and IDE usage data):
   - #1: **Rename** (symbol renaming)
   - #2: **Extract Method**
   - #3: **Extract Variable** (Inline Variable is the inverse)
   - #4: **Inline** (method/variable)
   - #5: **Move** (class/method between files)

2. **IDE Feature Comparison**:
   - **Visual Studio 2022**: 40+ refactorings, including AI-suggested refactorings
   - **JetBrains Rider**: 2500+ inspections and refactorings via ReSharper
   - **VS Code C# Extension**: 20+ common refactorings

3. **AI-Assisted Development Trends**:
   - Developers using AI assistants prefer simple, predictable refactorings
   - "Extract Method" and "Rename" are most requested in AI workflows
   - Users value clear error messages over comprehensive features

### Current Implementation Analysis

**Strengths**:
- ✅ Solid Roslyn-based implementation
- ✅ Excellent test coverage (114 tests, 86.5% line coverage)
- ✅ Good semantic analysis (data flow, dependency tracking)
- ✅ Proper error handling with categorization

**Gaps**:
- ❌ Missing Rename (highest priority refactoring)
- ❌ Missing Inline operations (common use case)
- ❌ Extract Method lacks return value detection
- ❌ Limited documentation of single-file constraints

---

## Appendix B: MCP Tool Definitions

Each refactoring will be exposed as an MCP tool with this general structure:

```json
{
  "name": "rename_symbol",
  "description": "Rename a local variable, parameter, private method, or private field within a single file",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": { "type": "string", "description": "Complete C# source code" },
      "symbolName": { "type": "string", "description": "Current name of the symbol" },
      "newName": { "type": "string", "description": "New name for the symbol" },
      "symbolType": { "type": "string", "enum": ["variable", "parameter", "method", "field"] }
    },
    "required": ["sourceCode", "symbolName", "newName"]
  }
}
```

All tools follow consistent patterns:
- Accept `sourceCode` as complete file content
- Return `RefactoringResult` with success/failure and refactored code
- Include clear error messages and warnings
- Specify limitations in tool description

---

## Document Approval

**Product Owner**: Approved - Ready for implementation
**Date**: 2025-10-09
**Next Review**: After Phase 1 completion (Week 2)

---

**END OF DOCUMENT**
