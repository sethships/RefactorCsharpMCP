# Product Requirements Document: RefactorCsharpMCP V1 Refactoring Capabilities

**Version:** 1.1.0
**Date:** 2025-10-09
**Status:** Final - Reviewed and Ready for Implementation
**Author:** Product Owner (Master)
**Reviewed by:** Master Software Architect

---

## Executive Summary

RefactorCsharpMCP V1 aims to deliver a focused, robust set of refactoring capabilities that address the most common C# code quality needs for AI-assisted development workflows. Rather than attempting comprehensive coverage of all possible refactorings, V1 prioritizes **breadth of common use cases** over **depth of edge cases**, ensuring each supported refactoring works reliably for typical development scenarios.

The V1 release will support **10 refactoring operations** across three categories: Code Extraction & Composition, Dependency Management, and Code Cleanup. These refactorings represent the operations developers perform most frequently and deliver the highest impact on code maintainability.

**Key Changes in v1.1.0:**
- Extended implementation timeline from 6 weeks to 7-8 weeks based on architect review
- Reordered Phase 1 to prioritize shared infrastructure and early wins
- Promoted Remove Unused Usings from P2 to P0 (moved to Phase 1)
- Enhanced Rename API with position-based symbol resolution
- Added Extract Class reference updating to Phase 2
- Incorporated shared infrastructure work (RefactoringBase, SymbolResolutionHelper)

---

## Goals & Objectives

### Primary Goals
1. **Enable AI-Assisted Refactoring**: Provide Claude Code and other MCP clients with reliable, production-ready C# refactoring capabilities
2. **Focus on Common Cases**: Support the 80% use case for each refactoring, deferring edge cases to future versions
3. **Ensure Correctness**: Every refactoring must preserve code semantics and compile successfully
4. **Maintain Simplicity**: Keep the API surface small and intuitive for AI clients to use effectively

### Success Criteria
- ✅ All 8 core refactorings work correctly for common cases (≥90% test coverage)
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
- ✅ **[ENHANCED in v1.1.0]** Automatic return value detection (single return and tuple returns)
- ✅ Preserve static/instance context (extracted method matches containing method)
- ✅ Support for local variables, method parameters, and primitive types
- ✅ Generic types (Dictionary<K,V>, List<T>, etc.)
- ✅ Nullable reference types (string?, List<int>?)
- ✅ Array types (int[], string[][])
- ✅ Instance field access (methods can access class fields directly)
- ✅ Single file refactoring only

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Extraction across multiple methods
- ❌ Async/await method extraction (detected but not auto-generated)
- ❌ LINQ expression extraction with proper closure handling
- ❌ Out/ref parameter handling
- ❌ Extract to different visibility levels (always private in V1)
- ❌ Cross-file extraction
- ❌ Partial class extraction
- ❌ Methods with early returns or complex control flow

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
- Return value correctly detected (void, single value, or tuple)
- Proper indentation and formatting

**Implementation Effort**: **3-4 days** (Medium complexity)

**Current Implementation Status**: ✅ Implemented, needs enhancement for return value detection

---

#### 1.2 Extract Class ✅ **[IMPLEMENTED - ENHANCE IN PHASE 2]**

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
- ✅ **[ENHANCED in Phase 2]** Automatic reference updating within same class
- ✅ Single file refactoring only

**V1 Scope (OUT OF SCOPE - Future Versions)**:
- ❌ Extract to separate file
- ❌ Extract with delegation pattern (auto-generate delegate methods)
- ❌ Interface extraction
- ❌ Inheritance-based extraction
- ❌ Constructor parameter injection for extracted class
- ❌ Cross-file reference updates

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

**⚠️ IMPORTANT**:
- **Phase 1**: Manual reference updates required (warning message displayed)
- **Phase 2**: Automatic reference updating for members within same class (calls to `GetFullAddress()` become `_address.GetFullAddress()`)

**Success Criteria**:
- New class created with extracted members
- Original class removes extracted members and adds new class field
- Code compiles (after automatic or manual reference updates)
- Phase 2: References within same class automatically updated

**Implementation Effort**:
- Phase 1 (current): ✅ Complete
- Phase 2 (reference updates): **3-4 days** (High value/effort ratio)

**Current Implementation Status**: ✅ Implemented with Phase 2 enhancement planned

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

**Implementation Effort**: **6-8 days** (High complexity due to parameter substitution and name conflict handling)

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

**Implementation Effort**: **4-5 days** (Medium complexity)

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

**Implementation Effort**: **5-6 days** (Medium-High complexity due to caller updates)

**Current Implementation Status**: ❌ Not implemented - NEW for V1 (Phase 4 - Optional)

---

### Category 3: Code Cleanup

#### 3.1 Make Field Readonly ✅ **[IMPLEMENTED - STABLE]**

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

#### 3.2 Safe Delete ✅ **[IMPLEMENTED - DOCUMENT LIMITATIONS]**

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

#### 3.3 Rename

**Description**: Rename a symbol (variable, method, class, field, property) and update all references consistently.

**Priority**: **P0** 🔥 - THE most frequently used refactoring operation (research shows higher frequency than Extract Method)

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

**Implementation Effort**: **5-7 days** (Medium-High complexity - symbol resolution and conflict detection)

**Current Implementation Status**: ❌ Not implemented - NEW for V1 (HIGHEST PRIORITY)

---

#### 3.4 Remove Unused Usings

**Description**: Remove using directives that are not referenced in the file.

**Priority**: **P0** 🔥 **[PROMOTED from P2 in v1.1.0]** - Easy win, moved to Phase 1

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

**Implementation Effort**: **2-3 days** (Low complexity - easiest refactoring to implement)

**Current Implementation Status**: ❌ Not implemented - NEW for V1 (moved to Phase 1)

---

## V1 Refactoring Summary Table

| # | Refactoring | Priority | Status | Category | Effort |
|---|-------------|----------|--------|----------|--------|
| 1 | **Remove Unused Usings** | P0 🔥 | ❌ NEW | Code Cleanup | 2-3 days |
| 2 | **Rename** | P0 🔥 | ❌ NEW | Code Cleanup | 5-7 days |
| 3 | Extract Method | P0 🔥 | ✅ ENHANCE | Code Extraction | 3-4 days |
| 4 | Constructor Injection | P0 🔥 | ✅ STABLE | Dependency Mgmt | - |
| 5 | Inline Variable | P1 | ❌ NEW | Code Extraction | 4-5 days |
| 6 | Inline Method | P1 | ❌ NEW | Code Extraction | 6-8 days |
| 7 | Make Field Readonly | P1 | ✅ STABLE | Code Cleanup | - |
| 8 | Safe Delete | P1 | ✅ DOCUMENT | Code Cleanup | - |
| 9 | Extract Class | P1 | ✅ ENHANCE | Code Extraction | 3-4 days |
| 10 | Introduce Parameter Object | P2 | ❌ NEW | Dependency Mgmt | 5-6 days |

**Total**: 10 refactorings (4 implemented, 6 new)

**Key Changes in v1.1.0**:
- Remove Unused Usings promoted from P2 to P0 (moved to Phase 1 for early win)
- Effort estimates updated based on architect review
- Extract Class includes Phase 2 enhancement for reference updates

---

## Implementation Priority & Phasing

### Phase 1: Critical Foundation (Weeks 1-3) **[EXTENDED in v1.1.0]**
**Goal**: Build shared infrastructure and implement high-priority refactorings

**Week 1: Infrastructure & Easy Win**
1. **Shared Infrastructure** (3 days)
   - RefactoringBase abstract class (eliminates boilerplate)
   - SymbolResolutionHelper utility (shared by multiple refactorings)
   - Enhanced error handling with ErrorCode enum

2. **Remove Unused Usings** (P0) (3 days) ← **MOVED UP from Phase 4**
   - Easiest refactoring provides early win and team confidence
   - No dependencies on other refactorings
   - Good warm-up for more complex work

**Week 2: Critical Symbol Resolution**
3. **Rename Symbol** (P0) (5 days)
   - Includes 2-day spike for symbol resolution prototype
   - Most frequently used refactoring (highest user value)
   - Foundation for other symbol-based refactorings

**Week 3: Extract Method Enhancement**
4. **Extract Method - Return Value Detection** (P0 enhancement) (4 days)
   - Complete existing implementation
   - Add single return and tuple return support
   - Comprehensive unit tests

**Phase 1 Deliverables**:
- Shared refactoring infrastructure (RefactoringBase, SymbolResolutionHelper)
- Remove Unused Usings with single-file scope
- Rename refactoring with single-file scope
- Extract Method with automatic return type detection (void, single, tuple)
- Comprehensive unit tests (>90% coverage for new code)
- Updated EXAMPLES.md

---

### Phase 2: Code Manipulation (Weeks 4-6)
**Goal**: Add P1 refactorings for complete common case coverage

**Week 4: Inline Operations**
1. **Inline Variable** (P1) (5 days)
   - Simpler than Inline Method
   - Standalone feature
   - Conservative approach: literals and simple expressions only

**Week 5: Extract Class Enhancement & Inline Method**
2. **Extract Class Enhancement** (P1) (4 days) ← **ADDED in v1.1.0**
   - Auto-update references within same class
   - High value/effort ratio
   - Reduces manual user work significantly

3. **Inline Method** (P1) - Start (4 days of 8-day task)
   - Most complex of Phase 2
   - Simple cases first (no parameters, no returns)

**Week 6: Inline Method Completion**
3. **Inline Method** (P1) - Complete (4 days remaining)
   - Parameter substitution
   - Variable conflict handling
   - Multiple call site support

**Phase 2 Deliverables**:
- Working implementations for Inline Variable and Inline Method
- Extract Class with automatic reference updates (within same class)
- Integration tests with real code samples
- Updated documentation with limitations

---

### Phase 3: Polish & Validation (Week 7)
**Goal**: Document limitations, validate performance, and prepare for release

1. **Documentation Updates** (2 days)
   - Document all limitations clearly (single-file scope, etc.)
   - Update README.md with limitation warnings
   - Update TROUBLESHOOTING.md
   - Create comprehensive EXAMPLES.md for all refactorings

2. **Performance Benchmarking** (2 days)
   - Create BenchmarkDotNet baseline measurements
   - Validate 2-second target for typical files
   - Document performance characteristics

3. **Test Coverage Validation** (1 day)
   - Ensure ≥90% coverage for all new refactorings
   - Minimum 10 unit tests per refactoring
   - Snapshot testing for complex outputs

**Phase 3 Deliverables**:
- Complete documentation with all limitations
- Performance benchmarks validating targets
- Test coverage reports
- Release-ready codebase

---

### Phase 4: Nice-to-Have (Week 8 - Optional)
**Goal**: Add P2 refactorings if time permits (can be deferred to V1.1)

1. **Introduce Parameter Object** (P2) (6 days)
   - Complex caller updates required
   - Defer to V1.1 if timeline slips

2. **Code Organization Refactoring** (2 days)
   - Reorganize into categorized folders (optional improvement)
   - Can be deferred to post-V1 release

**Phase 4 Deliverables**: Optional additions, can be deferred to V1.1 or V1.2

---

**Total Implementation Timeline: 7-8 weeks** (extended from original 6 weeks)

**Key Changes in v1.1.0 Phasing**:
- Phase 1 extended from 2 weeks to 3 weeks (architect recommendation)
- Added explicit shared infrastructure work in Week 1
- Moved Remove Unused Usings to Phase 1 for early win
- Reordered Rename to Week 2 (after infrastructure)
- Added Extract Class enhancement to Phase 2
- Total timeline: 7-8 weeks (vs original 6 weeks)

---

## Success Metrics

### Functional Metrics
- ✅ All P0 refactorings implemented and tested (4/4: Rename, Extract Method, Constructor Injection, Remove Unused Usings)
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
| Symbol resolution ambiguity (Rename) | High | Medium | Use position-based resolution (lineNumber + columnNumber) |

### Product Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Users expect cross-file refactoring (not supported) | Medium | High | **CLEARLY DOCUMENT** single-file limitation in all docs |
| Extract Class requires manual updates (Phase 1) | Medium | High | **Phase 2 enhancement** adds automatic reference updates |
| Safe Delete misses cross-file references | High | Medium | **WARNING MESSAGE** + recommendation to search |
| Rename complexity underestimated | High | Low | **2-day spike** before implementation, extended timeline |

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
   - **Recommendation**: Yes - tuple return types are stable in C# 7.0+
   - **Decision**: ✅ Include in Phase 1 enhancement

3. **Should we add "Extract Interface" to V1?**
   - **Recommendation**: No - P2 priority, complex implementation
   - **Decision**: Defer to V1.5

4. **Performance target: 2 seconds or 5 seconds?**
   - **Recommendation**: 2 seconds for typical files (<500 LOC), 5 seconds max timeout
   - **Decision**: Implement 5s timeout, optimize for 2s average

5. **Should Inline Method be included in V1 or deferred to V1.1?**
   - **Architect Note**: Complexity is higher than typical P1
   - **Decision**: Include in Phase 2 with clear scope limitations (simple methods only)

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

### Rename Symbol **[UPDATED in v1.1.0]**

```json
{
  "name": "rename_symbol",
  "description": "Rename a local variable, parameter, private method, or private field within a single file",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": { "type": "string", "description": "Complete C# source code" },
      "lineNumber": { "type": "number", "description": "1-based line number where symbol is located" },
      "columnNumber": { "type": "number", "description": "1-based column number where symbol starts" },
      "newName": { "type": "string", "description": "New name for the symbol" }
    },
    "required": ["sourceCode", "lineNumber", "columnNumber", "newName"]
  }
}
```

**Key Change**: Added `lineNumber` and `columnNumber` for precise symbol resolution (replaces ambiguous `symbolName` parameter)

### Inline Variable

```json
{
  "name": "inline_variable",
  "description": "Replace all uses of a variable with its initialization expression",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": { "type": "string", "description": "Complete C# source code" },
      "lineNumber": { "type": "number", "description": "Line where variable is declared" },
      "variableName": { "type": "string", "description": "Variable name (for validation)" }
    },
    "required": ["sourceCode", "lineNumber", "variableName"]
  }
}
```

### Inline Method

```json
{
  "name": "inline_method",
  "description": "Replace all calls to a method with the method's body",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": { "type": "string", "description": "Complete C# source code" },
      "className": { "type": "string", "description": "Class containing the method" },
      "methodName": { "type": "string", "description": "Method to inline" }
    },
    "required": ["sourceCode", "className", "methodName"]
  }
}
```

### Remove Unused Usings

```json
{
  "name": "remove_unused_usings",
  "description": "Remove using directives that are not referenced in the file",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": { "type": "string", "description": "Complete C# source code" }
    },
    "required": ["sourceCode"]
  }
}
```

### Introduce Parameter Object

```json
{
  "name": "introduce_parameter_object",
  "description": "Group related parameters into a parameter object class",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": { "type": "string", "description": "Complete C# source code" },
      "className": { "type": "string", "description": "Class containing the method" },
      "methodName": { "type": "string", "description": "Method with parameters to group" },
      "parameterNames": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Parameter names to group into object"
      },
      "newClassName": { "type": "string", "description": "Name for the parameter object class" }
    },
    "required": ["sourceCode", "className", "methodName", "parameterNames", "newClassName"]
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

**Product Owner**: Approved - Ready for implementation (v1.1.0)
**Reviewed by**: Master Software Architect - Approved with Recommendations
**Date**: 2025-10-09
**Version**: 1.1.0 (Incorporating architect feedback)
**Next Review**: After Phase 1 completion (Week 3)

**Key Updates in v1.1.0**:
- Extended timeline from 6 to 7-8 weeks
- Reordered Phase 1 implementation (infrastructure first, easy win, then complex work)
- Promoted Remove Unused Usings from P2 to P0
- Enhanced Rename API with position-based symbol resolution
- Added Extract Class reference updating to Phase 2
- Updated effort estimates based on technical complexity analysis
- Incorporated shared infrastructure work (RefactoringBase, SymbolResolutionHelper)

---

**END OF DOCUMENT**
