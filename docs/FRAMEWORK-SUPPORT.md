# Framework Version Support

**Version:** 1.0.0
**Status:** Active Documentation
**Related:** [DOT-NET-VERSION-SUPPORT.md](DOT-NET-VERSION-SUPPORT.md) (Technical Specification)

---

## Table of Contents

1. [Overview](#overview)
2. [Quick Start](#quick-start)
3. [Supported Frameworks](#supported-frameworks)
4. [Using Framework-Aware Refactorings](#using-framework-aware-refactorings)
5. [Known Limitations](#known-limitations)
6. [Troubleshooting](#troubleshooting)
7. [Migration from Pre-1.0](#migration-from-pre-10)
8. [Technical Reference](#technical-reference)

---

## Overview

RefactorCsharpMCP v1.0 introduces **framework version awareness**, ensuring refactored code is compatible with your target .NET framework. Different .NET frameworks support different C# language versions, which directly impacts the syntax and features available for refactoring.

### Why Framework Awareness Matters

**Without framework awareness:**
```csharp
// Refactoring might generate C# 12 code...
var items = [1, 2, 3];  // Collection expression (C# 12)

// ...that breaks on .NET Framework 4.8 (C# 7.3)
// Error CS8652: Collection expressions require C# 12+
```

**With framework awareness:**
```csharp
// Targeting net48 generates C# 7.3-compatible code
var items = new List<int> { 1, 2, 3 };  // Traditional syntax
```

---

## Quick Start

### 1. Identify Your Target Framework

Check your project file (`.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>  <!-- Use this value -->
  </PropertyGroup>
</Project>
```

Common values: `net8.0`, `net9.0`, `net48`, `net472`, `netstandard2.0`

### 2. Specify Framework in Refactoring Calls

**MCP Tool Call:**
```json
{
  "name": "extract_method",
  "arguments": {
    "sourceCode": "...",
    "startLine": 10,
    "endLine": 15,
    "newMethodName": "ProcessData",
    "targetFramework": "net8.0"  ← Required parameter
  }
}
```

**Direct C# API:**
```csharp
var refactoring = new ExtractMethod();
var result = await refactoring.ExecuteAsync(
    sourceCode,
    startLine: 10,
    endLine: 15,
    newMethodName: "ProcessData",
    targetFramework: "net8.0");  // Required
```

### 3. Handle Validation Errors

If you use an unsupported or end-of-life framework:

```json
{
  "success": false,
  "errorCode": "EOL_FRAMEWORK",
  "error": "Unsupported framework: .NET 6 reached end-of-life November 2024.",
  "suggestedFramework": "net8.0"
}
```

**Fix:** Update to the suggested framework or upgrade your project.

---

## Supported Frameworks

### Modern .NET (Primary Support)

| Framework | C# Version | Support Until | TFM |
|-----------|-----------|---------------|-----|
| .NET 9 | C# 13.0 | Nov 2026 (STS) | `net9.0` |
| .NET 8 | C# 12.0 | Nov 2026 (LTS) | `net8.0` |

**Recommendation:** Use `net8.0` for long-term support.

### .NET Framework (Windows-Only)

| Framework | C# Version | Support | TFM |
|-----------|-----------|---------|-----|
| .NET Framework 4.8.1 | C# 7.3 | Indefinite | `net481` |
| .NET Framework 4.8 | C# 7.3 | Indefinite | `net48` |
| .NET Framework 4.7.2 | C# 7.3 | Indefinite | `net472` |
| .NET Framework 4.7.1 | C# 7.3 | Indefinite | `net471` |
| .NET Framework 4.7 | C# 7.3 | Indefinite | `net47` |
| .NET Framework 4.6.2 | C# 7.3 | Indefinite | `net462` |
| .NET Framework 3.5 SP1 | C# 3.0 | Indefinite | `net35` |

**Note:** .NET Framework uses older C# versions with limited modern language features.

### .NET Standard (Cross-Platform Libraries)

| Framework | C# Version | TFM |
|-----------|-----------|-----|
| .NET Standard 2.1 | C# 8.0 | `netstandard2.1` |
| .NET Standard 2.0 | C# 7.3 | `netstandard2.0` |

**Use Case:** Building class libraries that work across .NET Framework and Modern .NET.

### End-of-Life Frameworks (NOT Supported)

| Framework | EOL Date | Use Instead |
|-----------|----------|-------------|
| .NET 7 | May 2024 | `net8.0` |
| .NET 6 | Nov 2024 | `net8.0` |
| .NET 5 | May 2022 | `net8.0` |
| .NET Core 3.1 | Dec 2022 | `net8.0` |
| .NET Framework 4.6.1 | Apr 2022 | `net462` |
| .NET Framework 4.6 | Apr 2022 | `net462` |
| .NET Framework 4.5.2 | Apr 2022 | `net462` |

**Error Behavior:** Tool returns `EOL_FRAMEWORK` error with suggested alternative.

---

## Using Framework-Aware Refactorings

### All Refactorings Support `targetFramework`

Every refactoring operation requires the `targetFramework` parameter:

1. **Extract Method** - `extract_method`
2. **Inline Method** - `inline_method`
3. **Inline Variable** - `inline_variable`
4. **Rename Symbol** - `rename_symbol`
5. **Constructor Injection** - `constructor_injection`
6. **Make Field Readonly** - `make_field_readonly`
7. **Safe Delete Method** - `safe_delete_method`
8. **Extract Class** - `extract_class`
9. **Remove Unused Usings** - `remove_unused_usings`

### Example: Extract Method Across Frameworks

**Input Code:**
```csharp
public void Process()
{
    var name = GetName();
    var age = GetAge();
    SaveUser(name, age);
}
```

**Targeting .NET 8 (C# 12):**
```json
{
  "targetFramework": "net8.0"
}
```

**Output (C# 12 features available):**
```csharp
public void Process()
{
    var (name, age) = GatherUserData();
    SaveUser(name, age);
}

private (string name, int age) GatherUserData()
{
    var name = GetName();
    var age = GetAge();
    return (name, age);  // Tuple return
}
```

**Targeting .NET Framework 4.8 (C# 7.3):**
```json
{
  "targetFramework": "net48"
}
```

**Output (C# 7.3 compatible):**
```csharp
public void Process()
{
    var (name, age) = GatherUserData();
    SaveUser(name, age);
}

private (string name, int age) GatherUserData()
{
    var name = GetName();
    var age = GetAge();
    return (name, age);  // Tuples supported with ValueTuple NuGet
}
```

**Targeting .NET Framework 3.5 (C# 3.0):**
```json
{
  "targetFramework": "net35"
}
```

**Output (Error - tuples not supported):**
```json
{
  "success": false,
  "errorCode": "UNSUPPORTED_LANGUAGE_FEATURE",
  "error": "Multiple return values require tuples (C# 7.0+). Target framework net35 uses C# 3.0.",
  "suggestion": "Extract single return value OR create custom return type OR upgrade to net472+"
}
```

### Framework-Specific Behavior Summary

| Feature | .NET 3.5 | .NET Fx 4.6.2-4.8.1 | .NET Std 2.1 | .NET 8/9 |
|---------|----------|---------------------|--------------|----------|
| Tuple returns | ❌ | ✅ | ✅ | ✅ |
| Nullable reference types | ❌ | ❌ | ✅ | ✅ |
| Collection expressions | ❌ | ❌ | ❌ | ✅ |
| Read-only auto-properties | ❌ | ✅ | ✅ | ✅ |
| String interpolation | ❌ | ✅ | ✅ | ✅ |
| Records | ❌ | ❌ | ❌ | ✅ |
| Global usings | ❌ | ❌ | ❌ | ✅ |

**See:** [DOT-NET-VERSION-SUPPORT.md](DOT-NET-VERSION-SUPPORT.md) for comprehensive feature matrix.

---

## Known Limitations

### .NET Framework 4.8 Reference Assembly Issues (Issue #75)

**Symptom:**
Some refactorings may fail when targeting .NET Framework 4.8 due to missing BCL reference assemblies in the Roslyn compilation environment.

**Affected Frameworks:**
- `net48`, `net481`, `net472`, `net471`, `net47`, `net462`

**Affected Refactorings:**
Most refactorings gracefully handle this limitation. Tests allow `net48` to fail with informative error messages.

**Error Example:**
```json
{
  "success": false,
  "errorCode": "COMPILATION_ERROR",
  "error": "Unable to resolve type 'System.Console'. Reference assembly limitations with net48.",
  "suggestion": "Ensure code compiles independently before refactoring, or target netstandard2.0 for cross-platform code."
}
```

**Workarounds:**

1. **Use .NET Standard 2.0** for cross-platform libraries:
   ```xml
   <TargetFramework>netstandard2.0</TargetFramework>
   ```

2. **Upgrade to Modern .NET** if possible:
   ```xml
   <TargetFramework>net8.0</TargetFramework>
   ```

3. **Simplify Code**: Ensure input code has minimal external dependencies

4. **Accept Limitation**: Some refactorings work perfectly on `net48`, others may fail gracefully

**Status:** Known limitation documented in [Issue #75](https://github.com/sethb75/RefactorCsharpMCP/issues/75). Framework validation tests allow `net48` failures to ensure graceful degradation.

### Single-File Scope (All Frameworks)

**Limitation:** Safe Delete and Rename Symbol only detect references within the same file.

**Impact:**
```csharp
// File1.cs
public class Service
{
    public void UnusedMethod() { }  // May be used in File2.cs
}

// File2.cs
var service = new Service();
service.UnusedMethod();  // Reference NOT detected
```

**Workaround:** Manually verify no cross-file references before deleting methods.

**Status:** Multi-file reference detection planned for future release.

### Language Version Mismatches

**Limitation:** Input code with modern C# syntax will fail validation when targeting older frameworks.

**Example:**
```csharp
// Input code (C# 12 collection expression)
var items = [1, 2, 3];

// Targeting net48 (C# 7.3)
{
  "success": false,
  "errorCode": "INPUT_SYNTAX_MISMATCH",
  "error": "Input code contains C# 12 collection expressions, incompatible with net48 (C# 7.3).",
  "suggestion": "Rewrite input code using C# 7.3 syntax or target net8.0+"
}
```

**Workaround:** Ensure input code matches or is older than target framework's C# version.

---

## Troubleshooting

### Error: "Missing required parameter 'targetFramework'"

**Cause:** `targetFramework` parameter not provided.

**Fix:** Add `targetFramework` to your refactoring call:
```json
{
  "targetFramework": "net8.0"  // Add this
}
```

### Error: "Invalid framework moniker"

**Cause:** Incorrect TFM format.

**Examples:**
- ❌ `dotnet8` → ✅ `net8.0`
- ❌ `.NET 8` → ✅ `net8.0`
- ❌ `netcore8.0` → ✅ `net8.0`
- ❌ `framework48` → ✅ `net48`

**Fix:** Use correct Target Framework Moniker format (see [Supported Frameworks](#supported-frameworks)).

### Error: "EOL_FRAMEWORK"

**Cause:** Attempting to use end-of-life .NET framework.

**Example:**
```json
{
  "errorCode": "EOL_FRAMEWORK",
  "error": "Unsupported framework: .NET 6 reached end-of-life November 2024.",
  "suggestedFramework": "net8.0"
}
```

**Fix:** Use the suggested framework or upgrade your project.

### Error: "UNSUPPORTED_LANGUAGE_FEATURE"

**Cause:** Refactoring requires C# feature not available in target framework.

**Example:**
```json
{
  "error": "Multiple return values require tuples (C# 7.0+). Target framework net35 uses C# 3.0.",
  "suggestion": "Extract single return value OR upgrade to net472+"
}
```

**Fix:**
1. Simplify refactoring to avoid modern features, OR
2. Upgrade target framework to newer version

### Validation Fails on .NET Framework 4.8

**Cause:** Reference assembly limitations (see [Known Limitations](#net-framework-48-reference-assembly-issues-issue-75)).

**Fix:**
1. Target `netstandard2.0` instead of `net48`
2. Simplify input code dependencies
3. Upgrade to modern .NET if possible

---

## Migration from Pre-1.0

### Breaking Change: Required `targetFramework` Parameter

**Pre-1.0 (Hypothetical):**
```json
{
  "sourceCode": "...",
  "startLine": 10,
  "endLine": 15,
  "newMethodName": "ProcessData"
}
```

**v1.0+:**
```json
{
  "sourceCode": "...",
  "startLine": 10,
  "endLine": 15,
  "newMethodName": "ProcessData",
  "targetFramework": "net8.0"  ← NEW REQUIRED PARAMETER
}
```

### Update Checklist

- [ ] Identify target framework from project file (`.csproj`)
- [ ] Add `targetFramework` parameter to all refactoring calls
- [ ] Update error handling for framework validation errors:
  - `INVALID_TFM_FORMAT`
  - `EOL_FRAMEWORK`
  - `UNSUPPORTED_LANGUAGE_FEATURE`
  - `INPUT_SYNTAX_MISMATCH`
- [ ] Test with your actual target framework before deploying

### AI Agent Integration

**Recommended Pattern:**
```javascript
// 1. Detect framework from project file
const targetFramework = detectFrameworkFromProjectFile("MyProject.csproj");

// 2. Validate framework is supported
const validationResult = await mcpServer.call("validate_framework", {
  framework: targetFramework
});

if (!validationResult.isValid) {
  // Prompt user to update project or use suggested framework
  targetFramework = validationResult.suggestedFramework;
}

// 3. Include in all refactoring calls
const result = await mcpServer.call("extract_method", {
  sourceCode: code,
  startLine: 10,
  endLine: 15,
  newMethodName: "ProcessData",
  targetFramework: targetFramework  // Always include
});
```

---

## Technical Reference

For comprehensive technical details, see:

- **[DOT-NET-VERSION-SUPPORT.md](DOT-NET-VERSION-SUPPORT.md)** - Complete framework compatibility matrix, version-specific refactoring behavior, C# feature availability, test strategy
- **[SDD-Framework-Version-Awareness.md](SDD-Framework-Version-Awareness.md)** - Software design document, architecture, implementation details
- **[PRD-Framework-Version-Awareness.md](PRD-Framework-Version-Awareness.md)** - Product requirements, use cases, acceptance criteria

### C# Language Version Reference

| .NET Framework | Default C# Version |
|----------------|--------------------|
| .NET 9 | C# 13.0 |
| .NET 8 | C# 12.0 |
| .NET 7 (EOL) | C# 11.0 |
| .NET 6 (EOL) | C# 10.0 |
| .NET 5 (EOL) | C# 9.0 |
| .NET Standard 2.1 | C# 8.0 |
| .NET Framework 4.x | C# 7.3 |
| .NET Standard 2.0 | C# 7.3 |
| .NET Framework 3.5 | C# 3.0 |

**Source:** Microsoft [C# Language Versioning](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/configure-language-version)

---

## FAQs

### Q: What happens if I don't specify `targetFramework`?

**A:** The refactoring will fail with error code `MISSING_REQUIRED_PARAMETER`. All refactorings require explicit framework specification in v1.0+.

### Q: Can I use a newer framework than my project targets?

**A:** Yes, but the refactored code may contain syntax not compatible with your actual project framework. Always use your project's actual target framework.

### Q: Why doesn't the tool auto-detect my framework from `.csproj`?

**A:** RefactorCsharpMCP is a **source code refactoring tool**, not a project file parser. It operates on source code strings without file system access. The calling application (AI agent, IDE extension) is responsible for reading the project file and passing the framework parameter.

### Q: Do I need to install different versions of the tool for different frameworks?

**A:** No. A single installation supports all frameworks. Just specify your target framework in the `targetFramework` parameter.

### Q: What if I need to refactor code for multiple target frameworks?

**A:** Call the refactoring tool separately for each framework:
```csharp
var net48Result = await refactoring.ExecuteAsync(code, targetFramework: "net48");
var net80Result = await refactoring.ExecuteAsync(code, targetFramework: "net8.0");
```

Each result will contain framework-appropriate syntax.

---

**Document Owner:** Product Team
**Last Updated:** 2025-11-15 (v1.0.0 Release)
**Next Review:** After user feedback collection

