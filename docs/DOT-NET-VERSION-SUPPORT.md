# .NET Version Support & Refactoring Compatibility Matrix

**Version:** 1.1.0 **[UPDATED]**
**Date:** 2025-10-10
**Status:** Comprehensive Analysis - Updated with Architect Recommendations
**Related:** PRD-V1-Refactoring-Capabilities.md (v1.3.0), PRD-Framework-Version-Awareness.md

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Supported .NET Versions](#supported-net-versions)
3. [C# Language Version Mapping](#c-language-version-mapping)
4. [Refactoring Compatibility Matrix](#refactoring-compatibility-matrix)
5. [Version-Specific Analysis by Refactoring](#version-specific-analysis-by-refactoring)
6. [Framework Detection Strategy](#framework-detection-strategy)
7. [API Design Considerations](#api-design-considerations)
8. [Testing Strategy](#testing-strategy)
9. [Migration Guide](#migration-guide)

---

## Executive Summary

RefactorCsharpMCP must support all Microsoft-supported .NET versions as of January 2025. This document provides a comprehensive analysis of how each of the 10 refactoring operations behaves across different .NET framework versions, accounting for:

- **C# language feature availability** (varies by framework)
- **Syntax differences** between frameworks
- **Version-specific validation rules**
- **Input/output code variations**
- **Edge cases and limitations per version**

**Critical Insight:** Not all refactorings behave identically across frameworks. C# language version determines available syntax, which directly impacts refactoring output.

---

## Supported .NET Versions

### Microsoft-Supported Frameworks (January 2025)

#### Modern .NET
| Framework | C# Version | Support End | TFM | Priority |
|-----------|-----------|-------------|-----|----------|
| .NET 9 | C# 13.0 | Nov 2026 (STS) | `net9.0` | P0 |
| .NET 8 | C# 12.0 | Nov 2026 (LTS) | `net8.0` | P0 |

#### .NET Framework (Windows Component Lifecycle)
| Framework | C# Version | Support | TFM | Priority |
|-----------|-----------|---------|-----|----------|
| .NET Framework 4.8.1 | C# 7.3 | Indefinite | `net481` | P0 |
| .NET Framework 4.8 | C# 7.3 | Indefinite | `net48` | P0 |
| .NET Framework 4.7.2 | C# 7.3 | Indefinite | `net472` | P0 |
| .NET Framework 4.7.1 | C# 7.3 | Indefinite | `net471` | P1 |
| .NET Framework 4.7 | C# 7.3 | Indefinite | `net47` | P1 |
| .NET Framework 4.6.2 | C# 7.3 | Indefinite | `net462` | P1 |
| .NET Framework 3.5 SP1 | C# 3.0 | Indefinite | `net35` | P2 |

#### .NET Standard (Cross-Platform Compatibility)
| Framework | C# Version | Support | TFM | Priority |
|-----------|-----------|---------|-----|----------|
| .NET Standard 2.1 | C# 8.0 | Active | `netstandard2.1` | P1 |
| .NET Standard 2.0 | C# 7.3 | Active | `netstandard2.0` | P1 |

**Total Supported:** 13 framework monikers

### End-of-Life Frameworks (NOT Supported)

| Framework | EOL Date | Suggested Alternative |
|-----------|----------|----------------------|
| .NET 7 | May 2024 | `net8.0` |
| .NET 6 | Nov 2024 | `net8.0` |
| .NET 5 | May 2022 | `net8.0` |
| .NET Core 3.1 | Dec 2022 | `net8.0` |
| .NET Framework 4.6.1 | Apr 2022 | `net462` |
| .NET Framework 4.6 | Apr 2022 | `net462` |
| .NET Framework 4.5.2 | Apr 2022 | `net462` |

**Tool Behavior:** Rejects EOL frameworks with error code `EOL_FRAMEWORK` and suggests nearest supported version.

---

## C# Language Version Mapping

### C# Feature Availability by Version

| C# Version | Key Features | .NET Frameworks |
|-----------|-------------|-----------------|
| **C# 13.0** | Params collections, Lock object, Method group natural type | .NET 9 |
| **C# 12.0** | Primary constructors, Collection expressions, Inline arrays, Optional lambda parameters | .NET 8 |
| **C# 11.0** | Raw string literals, Required members, List patterns | .NET 7 (EOL) |
| **C# 10.0** | Global usings, File-scoped namespaces, Record structs | .NET 6 (EOL) |
| **C# 9.0** | Records, Init-only setters, Top-level statements | .NET 5 (EOL) |
| **C# 8.0** | Nullable reference types, Async streams, Default interface methods, Using declarations | .NET Standard 2.1, .NET Core 3.x (EOL) |
| **C# 7.3** | Tuple equality, Pattern-based fixed, Generic constraints | .NET Framework 4.6.2-4.8.1, .NET Standard 2.0 |
| **C# 7.0** | Tuples, Pattern matching, Local functions, Out variables, Ref returns | .NET Framework 4.7+ (with NuGet packages) |
| **C# 6.0** | Expression-bodied members, String interpolation, Null-conditional operators, Auto-property initializers | .NET Framework 4.6+ |
| **C# 5.0** | Async/await | .NET Framework 4.5+ |
| **C# 4.0** | Dynamic binding, Named arguments, Optional parameters | .NET Framework 4.0+ |
| **C# 3.0** | LINQ, Lambda expressions, Anonymous types, Object initializers | .NET Framework 3.5 |

### Critical Feature Boundaries for Refactoring

#### .NET Framework 4.6.2-4.8.1 (C# 7.3)
**Available:**
- Tuples (with `System.ValueTuple` NuGet)
- Out variables
- Pattern matching (basic)
- Expression-bodied members
- Async/await
- Readonly structs

**NOT Available:**
- Nullable reference types (C# 8)
- Records (C# 9)
- Init-only setters (C# 9)
- Top-level statements (C# 9)
- File-scoped namespaces (C# 10)
- Primary constructors (C# 12)
- Collection expressions (C# 12)

#### .NET Standard 2.0 (C# 7.3)
**Same as .NET Framework 4.6.2-4.8.1** - Targets maximum compatibility

#### .NET Standard 2.1 (C# 8.0)
**Available:** All C# 7.3 features PLUS:
- Nullable reference types
- Using declarations
- Async streams
- Default interface methods

**NOT Available:** C# 9+ features

#### .NET 8/9 (C# 12/13)
**All modern C# features available**

---

## Refactoring Compatibility Matrix

### Summary Table

| Refactoring | .NET Fx 3.5 | .NET Fx 4.6.2-4.8.1 | .NET Std 2.0 | .NET Std 2.1 | .NET 8/9 | Notes |
|-------------|-------------|---------------------|--------------|--------------|----------|-------|
| **Extract Method** | ⚠️ Limited | ✅ Full | ✅ Full | ✅ Full | ✅ Full | C# 3.0 limits tuple returns |
| **Constructor Injection** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full | Fields/properties work across all versions |
| **Make Field Readonly** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full | Modifier-only change |
| **Safe Delete** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full | No syntax dependencies |
| **Extract Class** | ⚠️ Limited | ✅ Full | ✅ Full | ✅ Full | ✅ Enhanced | C# 3.0 limits object init |
| **Inline Method** | ⚠️ Limited | ✅ Full | ✅ Full | ✅ Full | ✅ Enhanced | **[UPDATED v1.1.0]** Version-sensitive for C# 6.0+ syntax |
| **Inline Variable** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Enhanced | Collection expressions in C# 12 |
| **Rename** | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full | No syntax dependencies |
| **Remove Unused Usings** | ✅ Full | ✅ Full | ✅ Full | ⚠️ Careful | ⚠️ Careful | Global usings C# 10+ |
| **Introduce Parameter Object** | ⚠️ Limited | ✅ Full | ✅ Full | ✅ Enhanced | ✅ Enhanced | Records (C# 9), primary ctors (C# 12) |

**Legend:**
- ✅ **Full** - Works with all common cases for this version
- ✅ **Enhanced** - Works fully + can use modern syntax if available
- ⚠️ **Limited** - Works but constrained by language version
- ⚠️ **Careful** - Requires version-specific handling

---

## Version-Specific Analysis by Refactoring

### 1. Extract Method

#### Framework Variation Points

##### Return Value Handling

**C# 12+ (.NET 8/9) - Collection Expressions:**
```csharp
// Input (lines 1-3)
var items = new List<int>();
items.Add(1);
items.Add(2);

// Output (.NET 8)
private List<int> CreateItems()
{
    return [1, 2];  // Collection expression (C# 12)
}

// Output (.NET Framework 4.8)
private List<int> CreateItems()
{
    var items = new List<int>();
    items.Add(1);
    items.Add(2);
    return items;  // Traditional initialization
}
```

**C# 7.0+ - Tuple Returns (Multi-Value):**
```csharp
// Input (lines 1-4)
var name = GetName();
var age = GetAge();
var email = GetEmail();
var valid = ValidateData(name, age, email);

// Output (.NET Framework 4.8 with ValueTuple NuGet)
private (string name, int age, string email, bool valid) GatherUserData()
{
    var name = GetName();
    var age = GetAge();
    var email = GetEmail();
    var valid = ValidateData(name, age, email);
    return (name, age, email, valid);
}

// Output (.NET Framework 3.5 - NO TUPLES)
// NOT SUPPORTED - Error message:
// "Multiple return values require C# 7.0+ (tuples). Target framework net35 (C# 3.0) does not support tuples.
//  Consider extracting to a custom class with properties."
```

**C# 3.0 (.NET Framework 3.5) - Must Use Custom Types:**
```csharp
// Input (lines 1-3)
var discount = CalculateDiscount(order);
var tax = CalculateTax(order.Total - discount);
var final = order.Total - discount + tax;

// Output (.NET Framework 3.5) - Create helper class
private decimal CalculateFinalAmount(Order order)
{
    var discount = CalculateDiscount(order);
    var tax = CalculateTax(order.Total - discount);
    var final = order.Total - discount + tax;
    return final;  // Single return value only
}

// If multiple returns needed in C# 3.0, tool must generate:
private class PricingResult  // Generated helper class
{
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Final { get; set; }
}
```

##### Nullable Reference Types

**C# 8.0+ (.NET Standard 2.1, .NET 8/9):**
```csharp
// Input with nullable context enabled
string? name = GetName();  // Nullable reference type
if (name != null)
{
    Process(name);
}

// Output (.NET 8 with nullable annotations)
private void ProcessName(string? name)  // Nullable preserved
{
    if (name != null)
    {
        Process(name);
    }
}

// Output (.NET Framework 4.8 - NO nullable reference types)
private void ProcessName(string name)  // Just 'string', no '?'
{
    if (name != null)
    {
        Process(name);
    }
}
```

##### Validation Rules by Version

| Validation Rule | .NET Fx 3.5 | .NET Fx 4.6.2-4.8.1 | .NET Std 2.1 | .NET 8/9 |
|----------------|-------------|---------------------|--------------|----------|
| Single return value | ✅ | ✅ | ✅ | ✅ |
| Tuple return (2+ values) | ❌ Error | ✅ | ✅ | ✅ |
| Nullable reference types | ❌ Stripped | ❌ Stripped | ✅ | ✅ |
| Collection expressions | ❌ | ❌ | ❌ | ✅ |
| Async methods | ❌ | ✅ (detected, warning) | ✅ | ✅ |

##### Edge Cases

**Case 1: Tuple Returns on .NET Framework 3.5**
- **Input:** Code with multiple return values
- **Behavior:** Tool REJECTS extraction with error:
  - `errorCode`: `UNSUPPORTED_LANGUAGE_FEATURE`
  - `error`: "Multiple return values require tuple support (C# 7.0+). Target framework net35 uses C# 3.0."
  - `suggestion`: "Extract to single return value OR create custom return type OR upgrade to .NET Framework 4.7.2+"

**Case 2: Async Method Extraction**
- **Input:** Code block containing `await` keyword
- **Behavior (.NET Framework 4.6.2+, .NET 8):** Tool detects async context:
  - Extracts method as `async Task` or `async Task<T>`
  - Preserves `await` keyword
  - Warns: "Extracted method is async. Ensure calling method is also async."
- **Behavior (.NET Framework 3.5):** Tool REJECTS:
  - `error`: "Async/await requires C# 5.0+. Target framework net35 uses C# 3.0."

**Case 3: Collection Expression in Extracted Method**
- **Input (.NET 8):** `var items = [1, 2, 3];`
- **Behavior:**
  - If `targetFramework="net8.0"` → Preserves collection expression
  - If `targetFramework="net48"` → Converts to `new List<int> { 1, 2, 3 }`

---

### 2. Constructor Injection

#### Framework Variation Points

##### Property Injection Style

**C# 6.0+ (.NET Framework 4.6.2+) - Read-Only Auto-Properties:**
```csharp
// Output (.NET Framework 4.8, .NET 8)
public class OrderService
{
    public ILogger Logger { get; }  // Read-only auto-property (C# 6)
    public IDatabase Database { get; }

    public OrderService(ILogger logger, IDatabase database)
    {
        Logger = logger;
        Database = database;
    }
}
```

**C# 3.0 (.NET Framework 3.5) - Explicit Backing Fields:**
```csharp
// Output (.NET Framework 3.5)
public class OrderService
{
    private readonly ILogger _logger;
    private readonly IDatabase _database;

    // Properties must have explicit backing fields in C# 3.0
    public ILogger Logger
    {
        get { return _logger; }
    }

    public IDatabase Database
    {
        get { return _database; }
    }

    public OrderService(ILogger logger, IDatabase database)
    {
        _logger = logger;
        _database = database;
    }
}
```

##### Nullable Reference Types (C# 8.0+)

**C# 8.0+ (.NET Standard 2.1, .NET 8/9):**
```csharp
#nullable enable  // Project has nullable context enabled

// Input method parameter
public void Initialize(ILogger? logger)  // Nullable parameter
{
    if (logger != null)
    {
        logger.Log("Initialized");
    }
}

// Output (.NET 8) - Nullable preserved
public class MyService
{
    private readonly ILogger? _logger;  // Nullable field

    public MyService(ILogger? logger)   // Nullable parameter
    {
        _logger = logger;
    }
}

// Output (.NET Framework 4.8) - Nullable stripped
public class MyService
{
    private readonly ILogger _logger;  // NO nullable annotation

    public MyService(ILogger logger)
    {
        _logger = logger;
    }
}
```

##### Required Members (C# 11.0+)

**NOT USED in Constructor Injection refactoring** - C# 11 `required` keyword is for object initializers, not constructor injection.

However, tool must NOT generate `required` in .NET Framework code:
```csharp
// INCORRECT for .NET Framework 4.8:
public class MyService
{
    public required ILogger Logger { get; init; }  // ❌ C# 11 feature
}

// CORRECT for .NET Framework 4.8:
public class MyService
{
    public ILogger Logger { get; }  // ✅ C# 6 read-only

    public MyService(ILogger logger)
    {
        Logger = logger;
    }
}
```

##### Validation Rules by Version

| Validation Rule | .NET Fx 3.5 | .NET Fx 4.6.2-4.8.1 | .NET Std 2.1 | .NET 8/9 |
|----------------|-------------|---------------------|--------------|----------|
| Private readonly fields | ✅ | ✅ | ✅ | ✅ |
| Read-only auto-properties | ❌ Use fields | ✅ | ✅ | ✅ |
| Nullable reference types | ❌ Stripped | ❌ Stripped | ✅ | ✅ |
| Init-only setters | ❌ | ❌ | ❌ | ✅ |
| Required members | ❌ | ❌ | ❌ | ✅ |

#### Edge Cases

**Case 1: Properties on .NET Framework 3.5**
- **Input:** User requests `useProperties=true`
- **Behavior:**
  - Generate properties with explicit get accessors and backing fields
  - DO NOT use C# 6 read-only auto-properties
  - Constructor assigns to backing fields

**Case 2: Nullable Context on .NET Framework**
- **Input:** Code has `#nullable enable` and nullable parameter types
- **Behavior (.NET Framework 4.8):**
  - Strip nullable reference type annotations (`ILogger?` → `ILogger`)
  - Preserve nullable value types (`int?` → `int?`)
  - Add comment: `// Note: Nullable reference types not available in C# 7.3`

---

### 3. Make Field Readonly

#### Framework Variation Points

This refactoring is **version-independent** - the `readonly` keyword has been available since C# 1.0.

**Identical across all versions:**
```csharp
// Input
private int _count;

public MyClass()
{
    _count = 0;
}

// Output (all .NET versions)
private readonly int _count;

public MyClass()
{
    _count = 0;
}
```

##### Readonly Structs (C# 7.2+)

**NOT part of V1 scope** - Making struct readonly is a different refactoring.

However, tool must be aware:
```csharp
// .NET Framework 4.8 or .NET 8
public readonly struct Point  // ✅ C# 7.2+ feature
{
    public readonly int X { get; }
    public readonly int Y { get; }
}

// .NET Framework 3.5 - Cannot use readonly struct
public struct Point  // NO readonly modifier
{
    private readonly int _x;
    private readonly int _y;

    public int X { get { return _x; } }
    public int Y { get { return _y; } }
}
```

##### Validation Rules (Universal)

| Validation Rule | All .NET Versions |
|----------------|-------------------|
| Field assigned only in constructor | ✅ |
| Field with initializer | ✅ |
| Static readonly fields | ✅ |
| Instance readonly fields | ✅ |

#### Edge Cases

**No version-specific edge cases** - `readonly` works identically across C# 1.0 through C# 13.

---

### 4. Safe Delete

#### Framework Variation Points

This refactoring is **version-independent** - symbol deletion and reference detection work identically across all .NET versions.

**Identical validation:**
```csharp
// Checking references works the same in all versions
FindReferencesAsynchronous(symbol)
```

#### Edge Cases

**No version-specific edge cases** - Reference detection uses Roslyn's SemanticModel, which abstracts version differences.

**Important Limitations (apply to ALL versions):**
- Single-file reference checking only (V1)
- Cannot detect reflection usage
- Cannot detect dynamic invocation

---

### 5. Extract Class

#### Framework Variation Points

##### Object Initialization Syntax

**C# 3.0+ (.NET Framework 3.5+) - Object Initializers:**
```csharp
// Output (.NET Framework 4.8, .NET 8)
public class Customer
{
    private readonly Address _address = new Address();  // ✅ Field initializer
}

// OR with constructor
public class Customer
{
    private readonly Address _address;

    public Customer()
    {
        _address = new Address();  // ✅ Constructor initialization
    }
}
```

**Pre-C# 3.0 (.NET Framework 2.0) - Not Supported in V1**
- .NET Framework 2.0 is EOL (April 2016) → NOT SUPPORTED

##### Collection Initialization

**C# 12 (.NET 8) - Collection Expressions:**
```csharp
// Extracted class with collection
public class ShoppingCart
{
    private readonly List<Item> _items = [];  // ✅ Collection expression
}

// .NET Framework 4.8 - Traditional initialization
public class ShoppingCart
{
    private readonly List<Item> _items = new List<Item>();  // ✅ Traditional
}

// .NET Framework 3.5 - Constructor initialization
public class ShoppingCart
{
    private readonly List<Item> _items;

    public ShoppingCart()
    {
        _items = new List<Item>();  // Initialize in constructor
    }
}
```

##### Validation Rules by Version

| Validation Rule | .NET Fx 3.5 | .NET Fx 4.6.2-4.8.1 | .NET Std 2.1 | .NET 8/9 |
|----------------|-------------|---------------------|--------------|----------|
| Extract fields to new class | ✅ | ✅ | ✅ | ✅ |
| Extract methods to new class | ✅ | ✅ | ✅ | ✅ |
| Create composition field | ✅ | ✅ | ✅ | ✅ |
| Field initializers | ✅ | ✅ | ✅ | ✅ |
| Collection expressions | ❌ | ❌ | ❌ | ✅ |

#### Edge Cases

**Case 1: Collection Initialization on .NET Framework**
- **Input:** Extracted class has `List<T>` field
- **Behavior (.NET Framework 4.8):**
  - Use traditional initialization: `new List<T>()`
  - DO NOT use collection expression: `[]`

**Case 2: Reference Updates (Phase 2)**
- **Input:** Original class calls `GetFullAddress()`
- **Behavior (Phase 2):** Update to `_address.GetFullAddress()`
- **Version Dependency:** None - method calls work identically

---

### 6. Inline Method **[RECLASSIFIED in v1.1.0]**

#### Framework Variation Points

**IMPORTANT:** Inline Method is **VERSION-SENSITIVE** when inlining methods that contain modern C# syntax. The refactoring must convert modern syntax to framework-compatible equivalents when targeting older frameworks.

##### Expression-Bodied Members (C# 6.0+)

**C# 6.0+ (.NET Framework 4.6+) - Expression Bodies:**
```csharp
// Input
private int Sum(int a, int b) => a + b;

public int Calculate()
{
    return Sum(5, 10);
}

// Output (.NET Framework 4.8, .NET 8) - Inline expression directly
public int Calculate()
{
    return 5 + 10;  // Expression inlined
}

// Output (.NET Framework 3.5) - Must expand expression-bodied member
// If method being inlined uses => syntax, it must be expanded for C# 3.0
public int Calculate()
{
    return 5 + 10;  // Expression still works, but source method format may need conversion
}
```

##### Read-Only Auto-Properties (C# 6.0+)

**C# 6.0+ (.NET Framework 4.6+):**
```csharp
// Input - Method accesses read-only auto-property
public class Config
{
    public int Timeout { get; } = 30;

    private int GetTimeout() => Timeout;
}

// Output (.NET Framework 4.8) - Inline directly
public class Config
{
    public int Timeout { get; } = 30;

    public void Process()
    {
        var timeout = Timeout;  // Inlined
    }
}

// Output (.NET Framework 3.5) - If inlining into C# 3.0 code
// Tool must be aware property syntax differs
public class Config
{
    private readonly int _timeout = 30;
    public int Timeout { get { return _timeout; } }

    public void Process()
    {
        var timeout = Timeout;  // Inlined, but property definition different
    }
}
```

##### String Interpolation (C# 6.0+)

**C# 6.0+ (.NET Framework 4.6+):**
```csharp
// Input
private string FormatName(string first, string last) => $"Name: {first} {last}";

public void Display()
{
    var formatted = FormatName("John", "Doe");
    Console.WriteLine(formatted);
}

// Output (.NET Framework 4.8, .NET 8) - Inline string interpolation
public void Display()
{
    var formatted = $"Name: John Doe";  // ✅ String interpolation preserved
    Console.WriteLine(formatted);
}

// Output (.NET Framework 3.5) - Convert to string.Format
public void Display()
{
    var formatted = string.Format("Name: {0} {1}", "John", "Doe");  // ✅ Converted to C# 3.0
    Console.WriteLine(formatted);
}
```

##### Lambda Expressions and Closures

**C# 3.0+ (.NET Framework 3.5+) - Lambda Inlining:**
```csharp
// Input
private int GetMultiplier() => 2;

public void Process()
{
    var items = list.Select(x => x * GetMultiplier());
}

// Output (all versions supporting lambdas)
public void Process()
{
    var items = list.Select(x => x * 2);  // Inlined
}
```

##### Validation Rules by Version **[UPDATED v1.1.0]**

| Validation Rule | .NET Fx 3.5 | .NET Fx 4.6.2-4.8.1 | .NET Std 2.1 | .NET 8/9 |
|----------------|-------------|---------------------|--------------|----------|
| Inline simple method | ✅ | ✅ | ✅ | ✅ |
| Inline with parameters | ✅ | ✅ | ✅ | ✅ |
| Expression-bodied members | ⚠️ Convert | ✅ | ✅ | ✅ |
| Read-only auto-properties | ⚠️ Expand | ✅ | ✅ | ✅ |
| String interpolation | ⚠️ Convert | ✅ | ✅ | ✅ |
| Lambda parameter substitution | ✅ | ✅ | ✅ | ✅ |
| Modern C# features (C# 8+) | ❌ Error | ⚠️ Depends | ✅ | ✅ |

#### Edge Cases **[UPDATED v1.1.0]**

**Case 1: Expression-Bodied Member on .NET Framework 3.5**
- **Input:** `private int Sum(int a, int b) => a + b;`
- **Behavior:**
  - Parse expression body correctly
  - Inline the expression at call sites
  - Result works in C# 3.0 (simple expressions compatible)
  - **Note:** Method *definition* uses C# 6 syntax, but inlined *expression* is C# 3.0 compatible

**Case 2: String Interpolation on .NET Framework 3.5**
- **Input:** `private string Format(string name) => $"Hello {name}";`
- **Behavior:**
  - Detect string interpolation (C# 6.0 feature)
  - Convert to `string.Format("Hello {0}", name)` for C# 3.0
  - Inline converted expression at call sites

**Case 3: Method with Modern C# 12 Features**
- **Input (.NET 8):** Method using collection expressions
- **Behavior:**
  - If `targetFramework="net8.0"` → Inline preserves collection expression syntax
  - If `targetFramework="net48"` → Convert collection expressions to traditional syntax before inlining
  - If conversion not possible → ERROR:
    - `errorCode`: `FRAMEWORK_SYNTAX_MISMATCH`
    - `error`: "Cannot inline method using C# 12 collection expressions into C# 7.3 context without conversion"
    - `suggestion`: "Use syntax conversion or target newer framework"

---

### 7. Inline Variable

#### Framework Variation Points

##### Collection Expressions (C# 12)

**C# 12 (.NET 8) - Collection Expressions:**
```csharp
// Input (.NET 8)
var items = [1, 2, 3];  // Collection expression
ProcessItems(items);

// Output (.NET 8) - Inline collection expression
ProcessItems([1, 2, 3]);  // ✅ Collection expression preserved

// Output (.NET Framework 4.8) - Convert to traditional syntax
var items = new List<int> { 1, 2, 3 };  // Traditional initialization
ProcessItems(items);
// Inlining would produce:
ProcessItems(new List<int> { 1, 2, 3 });  // ✅ C# 3.0 compatible
```

##### Object Initializers

**C# 3.0+ - Object Initializers:**
```csharp
// Input (.NET Framework 4.8 or .NET 8)
var person = new Person { Name = "John", Age = 30 };
Save(person);

// Output (all versions with C# 3.0+)
Save(new Person { Name = "John", Age = 30 });  // ✅ Inline object initializer
```

##### Tuple Deconstruction (C# 7.0+)

**C# 7.0+ (.NET Framework 4.7+ with ValueTuple NuGet):**
```csharp
// Input
var (name, age) = GetPerson();  // Tuple deconstruction
Console.WriteLine(name);

// Output - CANNOT inline tuple deconstruction
// Refactoring REJECTS with error:
// "Cannot inline tuple deconstruction. Use separate variables."
```

##### Validation Rules by Version

| Validation Rule | .NET Fx 3.5 | .NET Fx 4.6.2-4.8.1 | .NET Std 2.1 | .NET 8/9 |
|----------------|-------------|---------------------|--------------|----------|
| Inline literal | ✅ | ✅ | ✅ | ✅ |
| Inline method call | ✅ | ✅ | ✅ | ✅ |
| Inline object initializer | ✅ | ✅ | ✅ | ✅ |
| Inline collection expression | ❌ | ❌ | ❌ | ✅ |
| Tuple deconstruction | ❌ Error | ⚠️ Error | ⚠️ Error | ⚠️ Error |

#### Edge Cases

**Case 1: Collection Expression Inlining**
- **Input (.NET 8):** `var nums = [1, 2];`
- **Behavior:**
  - If `targetFramework="net8.0"` → Inline as `[1, 2]`
  - If `targetFramework="net48"` → Convert to `new[] { 1, 2 }`

**Case 2: Complex Initialization**
- **Input:** `var config = new Config { Timeout = 30, Retries = 3 };`
- **Behavior (all versions):** Inline entire initializer expression

---

### 8. Rename

#### Framework Variation Points

This refactoring is **version-independent** - symbol renaming works identically across all .NET versions using Roslyn's semantic model.

**Identical behavior:**
```csharp
// Input (any .NET version)
private int _cnt;

public void Process()
{
    _cnt = 10;
    Console.WriteLine(_cnt);
}

// Output (any .NET version)
private int _itemCount;  // Renamed

public void Process()
{
    _itemCount = 10;
    Console.WriteLine(_itemCount);
}
```

##### Validation Rules (Universal)

| Validation Rule | All .NET Versions |
|----------------|-------------------|
| Rename local variable | ✅ |
| Rename parameter | ✅ |
| Rename private field | ✅ |
| Rename private method | ✅ |
| Name conflict detection | ✅ |

#### Edge Cases

**No version-specific edge cases** - Symbol resolution and renaming use Roslyn's SemanticModel.

---

### 9. Remove Unused Usings

#### Framework Variation Points

##### Global Usings (C# 10+)

**C# 10+ (.NET 6+, .NET 8/9) - Global Usings:**
```csharp
// global_usings.cs (.NET 8)
global using System;
global using System.Collections.Generic;

// MyClass.cs
public class MyClass
{
    public void DoWork()
    {
        Console.WriteLine("Hello");  // Uses System from global usings
    }
}

// Refactoring behavior (.NET 8):
// - Detects global usings in separate file
// - Does NOT remove global usings (project-level)
// - Only removes file-level usings
```

**C# 9 and earlier - No Global Usings:**
```csharp
// MyClass.cs (.NET Framework 4.8)
using System;
using System.Collections.Generic;  // Unused

public class MyClass
{
    public void DoWork()
    {
        Console.WriteLine("Hello");
    }
}

// Output (.NET Framework 4.8)
using System;  // Kept

public class MyClass
{
    public void DoWork()
    {
        Console.WriteLine("Hello");
    }
}
```

##### Implicit Usings (.NET 6+)

**NOT directly handled** - Implicit usings are SDK-generated, not in source files. Tool operates on source code only.

However, tool must account for implicitly available namespaces:
```csharp
// .NET 8 project with ImplicitUsings enabled
// NO using directives needed for System, System.Collections.Generic, etc.

public class MyClass
{
    public void DoWork()
    {
        Console.WriteLine("Hello");  // System.Console available implicitly
        var list = new List<int>();  // System.Collections.Generic available
    }
}

// Refactoring behavior:
// - If no using directives in file → nothing to remove
// - Does NOT add using directives for implicit usings
```

##### Validation Rules by Version

| Validation Rule | .NET Fx 3.5-4.8.1 | .NET Std 2.0-2.1 | .NET 6 (EOL) | .NET 8/9 |
|----------------|-------------------|------------------|--------------|----------|
| Remove unused file-level usings | ✅ | ✅ | ✅ | ✅ |
| Preserve global usings | ❌ N/A | ❌ N/A | ✅ | ✅ |
| Handle implicit usings | ❌ N/A | ❌ N/A | ⚠️ Aware | ⚠️ Aware |

#### Edge Cases

**Case 1: Global Usings on .NET 8**
- **Input:** File references `global_usings.cs`
- **Behavior:**
  - Tool detects global usings via compilation
  - Does NOT remove global usings (they're project-level)
  - Only removes unused file-level usings

**Case 2: Implicit Usings Enabled (.NET 8)**
- **Input:** File has no using directives, uses System types
- **Behavior:**
  - Tool recognizes types are available (via SemanticModel)
  - No usings to remove (success, no changes)

**Case 3: .NET Framework 4.8**
- **Input:** Standard using directives
- **Behavior:** Remove unused, preserve used (standard behavior)

---

### 10. Introduce Parameter Object

#### Framework Variation Points

##### Record Types (C# 9.0+)

**C# 9.0+ (.NET 5+, .NET 8/9) - Records:**
```csharp
// Input
public void CreateCustomer(string name, string email, string street, string city, string zip)
{
    // ...
}

// Output (.NET 8) - Record type
public record AddressInfo(string Street, string City, string Zip);

public void CreateCustomer(string name, string email, AddressInfo address)
{
    // Use address.Street, address.City, address.Zip
}

// Output (.NET Framework 4.8) - Traditional class
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

##### Primary Constructors (C# 12.0+)

**C# 12 (.NET 8) - Primary Constructors:**
```csharp
// Output (.NET 8) - Primary constructor
public class AddressInfo(string street, string city, string zip)
{
    public string Street { get; } = street;
    public string City { get; } = city;
    public string Zip { get; } = zip;
}

// Output (.NET Framework 4.8) - Traditional constructor
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
```

##### Init-Only Setters (C# 9.0+)

**C# 9.0+ (.NET 5+, .NET 8/9) - Init Setters:**
```csharp
// Output (.NET 8) - Init-only setters
public class AddressInfo
{
    public string Street { get; init; }  // C# 9 feature
    public string City { get; init; }
    public string Zip { get; init; }
}

// Output (.NET Framework 4.8) - Read-only properties
public class AddressInfo
{
    public string Street { get; }  // C# 6 read-only
    public string City { get; }
    public string Zip { get; }

    public AddressInfo(string street, string city, string zip)
    {
        Street = street;
        City = city;
        Zip = zip;
    }
}
```

##### Validation Rules by Version

| Validation Rule | .NET Fx 3.5 | .NET Fx 4.6.2-4.8.1 | .NET Std 2.1 | .NET 8/9 |
|----------------|-------------|---------------------|--------------|----------|
| Create class with properties | ✅ | ✅ | ✅ | ✅ |
| Record types | ❌ | ❌ | ❌ | ✅ |
| Primary constructors | ❌ | ❌ | ❌ | ✅ |
| Init-only setters | ❌ | ❌ | ❌ | ✅ |
| Read-only auto-properties | ❌ Use fields | ✅ | ✅ | ✅ |

#### Edge Cases

**Case 1: Modern Syntax Preference (.NET 8)**
- **Input:** Parameters to group
- **Behavior (.NET 8):**
  - Default: Generate record type (most concise)
  - Alternative: Generate class with primary constructor
  - Alternative: Generate traditional class

**Case 2: .NET Framework 4.8**
- **Input:** Parameters to group
- **Behavior (.NET Framework 4.8):**
  - Generate class with read-only auto-properties
  - Traditional constructor with assignments
  - NO records, NO init setters, NO primary constructors

**Case 3: .NET Framework 3.5**
- **Input:** Parameters to group
- **Behavior (.NET Framework 3.5):**
  - Generate class with explicit get-only properties
  - Backing fields for properties
  - Traditional constructor

---

## Framework Detection Strategy

### Explicit Framework Parameter (Required)

All MCP tools require `targetFramework` parameter:

```json
{
  "name": "extract_method",
  "inputSchema": {
    "properties": {
      "targetFramework": {
        "type": "string",
        "description": "Target framework moniker (e.g., 'net8.0', 'net48', 'net462')"
      }
    },
    "required": ["targetFramework"]
  }
}
```

**Rationale:**
- Zero ambiguity - caller specifies exact framework
- Predictable behavior - same input → same output
- Testable - all framework scenarios explicitly covered
- Caller responsibility - AI agent or user knows their framework

### Framework Validation Flow

```
1. User/Agent calls tool with targetFramework parameter
2. FrameworkValidator.Validate(targetFramework)
   ├─ Format validation (is it a valid TFM?)
   ├─ EOL detection (is it end-of-life?)
   └─ Support check (is it MS-supported?)
3. If valid: LanguageVersionMapper.GetLanguageVersion(targetFramework)
4. If invalid: Return error with errorCode and suggestion
5. If EOL: Return error with suggested framework
6. Configure Roslyn with correct C# version
7. Execute refactoring with version-appropriate syntax
```

### Error Responses by Framework Issue

#### Invalid Format
```json
{
  "success": false,
  "errorCode": "INVALID_TFM_FORMAT",
  "error": "Invalid framework moniker: 'dotnet8'. Use 'net8.0'.",
  "validExamples": ["net8.0", "net48", "net462"],
  "help": "Use list_supported_frameworks tool to see valid formats."
}
```

#### EOL Framework
```json
{
  "success": false,
  "errorCode": "EOL_FRAMEWORK",
  "error": "Unsupported framework: .NET 6 reached end-of-life November 2024.",
  "suggestedFramework": "net8.0",
  "workaround": "Specify 'net8.0' and manually verify compatibility."
}
```

#### Unknown Framework
```json
{
  "success": false,
  "errorCode": "UNKNOWN_FRAMEWORK",
  "error": "Unrecognized framework: 'net10.0'.",
  "suggestedFramework": "net9.0",
  "supportedFrameworks": ["net9.0", "net8.0", "net48", ...]
}
```

#### Input Syntax Mismatch **[NEW in v1.1.0]**
```json
{
  "success": false,
  "errorCode": "INPUT_SYNTAX_MISMATCH",
  "error": "Input code contains C# 12 collection expressions, which are incompatible with target framework net48 (C# 7.3).",
  "feature": "Collection expressions",
  "requiredVersion": "C# 12.0",
  "targetVersion": "C# 7.3",
  "suggestion": "Remove collection expressions from input code or target net8.0+"
}
```

#### Framework Syntax Mismatch **[NEW in v1.1.0]**
```json
{
  "success": false,
  "errorCode": "FRAMEWORK_SYNTAX_MISMATCH",
  "error": "Refactored code would generate C# 8.0 nullable reference types, which are incompatible with target framework net48 (C# 7.3).",
  "feature": "Nullable reference types",
  "requiredVersion": "C# 8.0",
  "targetVersion": "C# 7.3",
  "suggestion": "Target netstandard2.1 or net8.0 to use nullable reference types"
}
```

---

## API Design Considerations

### Version-Aware Refactoring Base Class

All refactorings inherit from version-aware base:

```csharp
public abstract class RefactoringBase
{
    protected FrameworkInfo FrameworkInfo { get; }
    protected LanguageVersion LanguageVersion { get; }

    protected RefactoringBase(string targetFramework)
    {
        var validation = FrameworkValidator.Validate(targetFramework);
        if (!validation.IsValid)
            throw new InvalidFrameworkException(validation.ErrorMessage);

        FrameworkInfo = validation.FrameworkInfo;
        LanguageVersion = LanguageVersionMapper.GetLanguageVersion(FrameworkInfo);
    }

    protected CSharpParseOptions CreateParseOptions()
    {
        return new CSharpParseOptions(LanguageVersion);
    }

    protected bool SupportsFeature(CSharpFeature feature)
    {
        return FeatureAvailability.IsAvailable(feature, LanguageVersion);
    }
}
```

### Feature Availability Checks

```csharp
public enum CSharpFeature
{
    Tuples,                  // C# 7.0
    NullableReferenceTypes,  // C# 8.0
    Records,                 // C# 9.0
    InitOnlySetters,         // C# 9.0
    FileScopedNamespaces,    // C# 10.0
    GlobalUsings,            // C# 10.0
    PrimaryConstructors,     // C# 12.0
    CollectionExpressions,   // C# 12.0
}

public static class FeatureAvailability
{
    public static bool IsAvailable(CSharpFeature feature, LanguageVersion version)
    {
        return feature switch
        {
            CSharpFeature.Tuples => version >= LanguageVersion.CSharp7,
            CSharpFeature.NullableReferenceTypes => version >= LanguageVersion.CSharp8,
            CSharpFeature.Records => version >= LanguageVersion.CSharp9,
            CSharpFeature.CollectionExpressions => version >= LanguageVersion.CSharp12,
            _ => false
        };
    }
}
```

### Example Usage in Extract Method

```csharp
public class ExtractMethodRefactoring : RefactoringBase
{
    public RefactoringResult Execute(...)
    {
        // Check if tuples supported for multi-value returns
        if (returnValues.Count > 1)
        {
            if (!SupportsFeature(CSharpFeature.Tuples))
            {
                return RefactoringResult.Failure(
                    ErrorCode.UNSUPPORTED_LANGUAGE_FEATURE,
                    $"Multiple return values require tuples (C# 7.0+). " +
                    $"Target framework {FrameworkInfo.Moniker} uses {LanguageVersion}.");
            }

            // Generate tuple return
            return GenerateTupleReturn(returnValues);
        }

        // Single return or void
        return GenerateSingleReturn(returnValue);
    }
}
```

---

## Testing Strategy

### Test Matrix Coverage

Each refactoring requires tests across framework versions:

| Test Category | .NET Fx 4.6.2 | .NET Fx 4.8 | .NET Std 2.0 | .NET Std 2.1 | .NET 8 | .NET 9 |
|--------------|---------------|-------------|--------------|--------------|--------|--------|
| Basic scenarios | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Version-specific syntax | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unsupported features | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Error handling | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

### Critical Test Cases

#### TC-1: Extract Method with Tuple Returns
```csharp
[Theory]
[InlineData("net48", true)]   // C# 7.3 supports tuples
[InlineData("net8.0", true)]  // C# 12 supports tuples
[InlineData("net35", false)]  // C# 3.0 does NOT support tuples
public void ExtractMethod_MultipleReturns_UsesTuplesOrFails(
    string targetFramework,
    bool shouldSucceed)
{
    var code = @"
        public void Process()
        {
            var name = GetName();
            var age = GetAge();
            Save(name, age);
        }";

    var result = ExtractMethodRefactoring.Execute(
        code,
        startLine: 3,
        endLine: 4,
        newMethodName: "GatherData",
        targetFramework: targetFramework);

    if (shouldSucceed)
    {
        Assert.True(result.Success);
        Assert.Contains("(string, int)", result.RefactoredCode);  // Tuple return
    }
    else
    {
        Assert.False(result.Success);
        Assert.Equal(ErrorCode.UNSUPPORTED_LANGUAGE_FEATURE, result.ErrorCode);
    }
}
```

#### TC-2: Constructor Injection Property Style
```csharp
[Theory]
[InlineData("net48", "public ILogger Logger { get; }")]     // C# 6 read-only
[InlineData("net35", "public ILogger Logger")]              // C# 3.0 explicit get
[InlineData("net8.0", "public ILogger Logger { get; }")]    // C# 12 read-only
public void ConstructorInjection_GeneratesCorrectPropertySyntax(
    string targetFramework,
    string expectedPropertySyntax)
{
    var result = ConstructorInjectionRefactoring.Execute(
        code,
        useProperties: true,
        targetFramework: targetFramework);

    Assert.Contains(expectedPropertySyntax, result.RefactoredCode);
}
```

#### TC-3: Remove Unused Usings with Global Usings
```csharp
[Theory]
[InlineData("net48", false)]   // No global usings in C# 7.3
[InlineData("net8.0", true)]   // Global usings in C# 10+
public void RemoveUnusedUsings_PreservesGlobalUsings(
    string targetFramework,
    bool hasGlobalUsings)
{
    var code = hasGlobalUsings
        ? "global using System;\n\nusing System.Linq; // unused"
        : "using System;\nusing System.Linq; // unused";

    var result = RemoveUnusedUsingsRefactoring.Execute(
        code,
        targetFramework: targetFramework);

    if (hasGlobalUsings)
    {
        Assert.Contains("global using System;", result.RefactoredCode);
    }

    Assert.DoesNotContain("using System.Linq;", result.RefactoredCode);
}
```

#### TC-4: EOL Framework Rejection
```csharp
[Theory]
[InlineData("net6.0", "EOL_FRAMEWORK", "net8.0")]
[InlineData("net452", "EOL_FRAMEWORK", "net462")]
[InlineData("netcoreapp3.1", "EOL_FRAMEWORK", "net8.0")]
public void AllRefactorings_RejectEOLFrameworks(
    string eolFramework,
    string expectedErrorCode,
    string suggestedFramework)
{
    var result = ExtractMethodRefactoring.Execute(
        code,
        targetFramework: eolFramework);

    Assert.False(result.Success);
    Assert.Equal(expectedErrorCode, result.ErrorCode);
    Assert.Equal(suggestedFramework, result.SuggestedFramework);
}
```

### Test Coverage Goals

- **Framework Validation:** 100% of supported + EOL frameworks tested
- **Feature Detection:** 100% of C# feature availability checks tested
- **Refactoring Execution:** ≥90% code coverage per refactoring
- **Error Handling:** 100% of error codes tested

---

## Migration Guide

### For Users Upgrading from V0.x (No Framework Parameter)

**Before (hypothetical V0.x):**
```json
{
  "sourceCode": "...",
  "startLine": 10,
  "endLine": 20,
  "newMethodName": "ProcessData"
}
```

**After (V1.0+):**
```json
{
  "sourceCode": "...",
  "startLine": 10,
  "endLine": 20,
  "newMethodName": "ProcessData",
  "targetFramework": "net8.0"  // REQUIRED
}
```

**Note:** RefactorCsharpMCP V1 is the **initial release** with framework awareness. There is no V0.x to migrate from. This section is for reference if future versions introduce breaking changes.

### For AI Agents

**Discovery Pattern:**
```
1. On first refactoring request:
   Agent → Server: list_supported_frameworks

2. Cache supported frameworks for session

3. For each refactoring:
   - Infer framework from project file OR ask human
   - Include targetFramework in all tool calls
   - Handle EOL_FRAMEWORK errors gracefully
```

**Error Handling Pattern:**
```
try:
    result = call_tool("extract_method", targetFramework="net6.0")
except:
    if error.errorCode == "EOL_FRAMEWORK":
        suggested = error.suggestedFramework
        ask_human(f"Your project uses EOL framework. Use {suggested} instead?")
    elif error.errorCode == "INVALID_TFM_FORMAT":
        show_valid_formats()
    else:
        report_error(error)
```

---

## Summary

### Key Takeaways

1. **Framework Awareness is Critical:** C# language version varies by .NET framework, directly affecting refactoring output.

2. **13 Supported Frameworks:** .NET 8-9, .NET Framework 4.6.2-4.8.1, .NET Framework 3.5, .NET Standard 2.0-2.1

3. **Version-Specific Refactoring Behavior:**
   - Extract Method: Tuple returns require C# 7.0+
   - Constructor Injection: Read-only auto-properties require C# 6.0+
   - Introduce Parameter Object: Records require C# 9.0+
   - Remove Unused Usings: Global usings awareness for C# 10+

4. **Explicit Framework Parameter:** All tools require `targetFramework` - no auto-detection, no fallbacks

5. **EOL Framework Rejection:** Tool rejects end-of-life frameworks with clear error and suggested alternative

6. **Testing Across Versions:** Each refactoring tested on minimum 6 framework versions

7. **AI Agent Integration:** Discovery tool (`list_supported_frameworks`) enables self-service framework learning

---

**Document Owner:** Product Owner (Master)
**Last Updated:** 2025-10-09
**Next Review:** After V1 implementation complete
