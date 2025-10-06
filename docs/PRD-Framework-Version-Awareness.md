# Product Requirements Document: .NET Framework Version Awareness for RefactorCsharpMCP

## 1. Executive Summary

### Problem Statement
RefactorCsharpMCP currently lacks awareness of target .NET framework versions and C# language versions when performing refactoring operations. This causes the tool to potentially generate code that:
- Uses modern C# syntax incompatible with the target framework (e.g., C# 12 features for .NET Framework 4.8 code)
- References APIs not available in the target framework
- Fails to compile in the original project context
- Breaks existing codebases during refactoring

### Scope
Add comprehensive .NET framework version detection and C# language version enforcement to all RefactorCsharpMCP refactoring operations, **supporting only Microsoft-supported .NET versions as of 2025**.

**Design Decision:** All MCP tools will require callers to explicitly specify the target framework via a **required `targetFramework` parameter**. This ensures:
- Zero ambiguity in tool behavior
- Predictable, testable refactoring results
- Clear contract between caller and tool
- Caller responsibility to know their framework version

### Non-Goals (Explicitly Out of Scope)
- ❌ Framework version upgrading (e.g., .NET Framework 4.6.2 → .NET 8)
- ❌ Code modernization suggestions
- ❌ Support for end-of-life .NET versions (e.g., .NET Core 2.x, .NET 5-7, .NET Framework 4.5.2-4.6.1)
- ❌ Migration tooling or recommendations
- ❌ Breaking change detection between frameworks
- ❌ Automatic framework detection from project files (may be added as separate future enhancement)
- ❌ Smart defaults or fallback framework versions

### Success Criteria
- All MCP tools accept required `targetFramework` parameter
- Generate refactored code compatible with specified framework
- Support **only Microsoft-supported** .NET versions (as of January 2025)
- Clear error messages when invalid framework specified
- Achieve 90%+ test coverage for version-aware refactoring

## 2. Supported .NET Versions (Microsoft-Supported Only)

Based on official Microsoft support policy as of January 2025:

### 2.1 Modern .NET (Currently Supported)

| Version | Release Date | End of Support | Type | C# Version | Status |
|---------|-------------|----------------|------|-----------|--------|
| .NET 9 | Nov 12, 2024 | Nov 10, 2026 | STS | 13.0 | ✅ SUPPORTED |
| .NET 8 | Nov 14, 2023 | Nov 10, 2026 | LTS | 12.0 | ✅ SUPPORTED |

**Implementation Priority:** P0 (Critical)

### 2.2 .NET Framework (Currently Supported)

| Version | Release Date | Type | C# Version | Status |
|---------|-------------|------|-----------|--------|
| .NET Framework 4.8.1 | Aug 9, 2022 | Latest | 7.3 | ✅ SUPPORTED |
| .NET Framework 4.8 | Apr 18, 2019 | Component | 7.3 | ✅ SUPPORTED |
| .NET Framework 4.7.2 | Apr 30, 2018 | Component | 7.3 | ✅ SUPPORTED |
| .NET Framework 4.7.1 | Oct 17, 2017 | Component | 7.3 | ✅ SUPPORTED |
| .NET Framework 4.7 | Apr 5, 2017 | Component | 7.3 | ✅ SUPPORTED |
| .NET Framework 4.6.2 | Aug 2, 2016 | Component | 7.3 | ✅ SUPPORTED |
| .NET Framework 3.5 SP1 | Nov 18, 2008 | Component | 3.0 | ✅ SUPPORTED |

**Implementation Priority:** P0 (Critical) for 4.6.2+, P1 (High) for 3.5 SP1

**Note:** .NET Framework support is tied to Windows OS lifecycle. These versions remain supported as long as they're on a supported Windows version.

### 2.3 .NET Standard (Actively Used)

| Version | C# Version | Status | Notes |
|---------|-----------|--------|-------|
| .NET Standard 2.1 | 8.0 | ✅ SUPPORTED | For .NET Core 3.x+ compatibility |
| .NET Standard 2.0 | 7.3 | ✅ SUPPORTED | For .NET Framework + .NET compatibility |

**Implementation Priority:** P1 (High)

**Note:** .NET Standard is not receiving new versions, but remains supported through implementing .NET versions. Use netstandard2.0 for cross-platform code targeting .NET Framework.

### 2.4 End-of-Life Versions (NOT Supported)

The following versions are **explicitly excluded** from this implementation:

| Version | EOL Date | Reason |
|---------|----------|--------|
| .NET 7 | May 14, 2024 | Out of support |
| .NET 6 | Nov 12, 2024 | Out of support |
| .NET 5 | May 10, 2022 | Out of support |
| .NET Core 3.1 | Dec 13, 2022 | Out of support |
| .NET Core 3.0 | Mar 3, 2020 | Out of support |
| .NET Core 2.2 | Dec 23, 2019 | Out of support |
| .NET Core 2.1 | Aug 21, 2021 | Out of support |
| .NET Core 2.0 | Oct 1, 2018 | Out of support |
| .NET Framework 4.6.1 | Apr 26, 2022 | Out of support |
| .NET Framework 4.6 | Apr 26, 2022 | Out of support |
| .NET Framework 4.5.2 | Apr 26, 2022 | Out of support |

**Implementation:** Tool should **detect but warn** when these versions are encountered, defaulting to a conservative supported version.

### 2.5 DevTools Repository Impact

**Current DevTools Projects vs Support Status:**

| Project | Current Framework | Support Status | Recommended Action |
|---------|------------------|----------------|-------------------|
| BackupTool | .NET Framework 4.5.2 | ❌ EOL (Apr 2022) | **Warn user**, fallback to 4.6.2 behavior |
| LineCounter | .NET Framework 4.5.2 | ❌ EOL (Apr 2022) | **Warn user**, fallback to 4.6.2 behavior |
| Logging | .NET Framework 4.5.2 | ❌ EOL (Apr 2022) | **Warn user**, fallback to 4.6.2 behavior |
| passgen | .NET 8 | ✅ Supported | Fully supported |
| RefactorCsharpMCP | .NET 8 | ✅ Supported | Fully supported |

**Important:** While we won't actively support .NET Framework 4.5.2, we'll handle it gracefully by treating it as 4.6.2 (C# 7.3) and issuing a warning.

## 3. Technical Design

### 3.1 Framework to C# Language Version Mapping

**Definitive Mapping Table (Microsoft-Supported Only):**

```csharp
private static readonly Dictionary<string, LanguageVersion> FrameworkLanguageMap = new()
{
    // Modern .NET (Supported)
    ["net9.0"] = LanguageVersion.CSharp13,
    ["net8.0"] = LanguageVersion.CSharp12,

    // .NET Framework (Supported)
    ["net481"] = LanguageVersion.CSharp7_3,
    ["net48"] = LanguageVersion.CSharp7_3,
    ["net472"] = LanguageVersion.CSharp7_3,
    ["net471"] = LanguageVersion.CSharp7_3,
    ["net47"] = LanguageVersion.CSharp7_3,
    ["net462"] = LanguageVersion.CSharp7_3,
    ["net35"] = LanguageVersion.CSharp3,  // .NET Framework 3.5 SP1

    // .NET Standard (Actively Used)
    ["netstandard2.1"] = LanguageVersion.CSharp8,
    ["netstandard2.0"] = LanguageVersion.CSharp7_3,
};

// EOL versions - detect and warn, fallback to nearest supported version
private static readonly Dictionary<string, string> EOLFrameworkFallbacks = new()
{
    // .NET Framework EOL → Fallback to 4.6.2
    ["net461"] = "net462",
    ["net46"] = "net462",
    ["net452"] = "net462",
    ["net451"] = "net462",
    ["net45"] = "net462",

    // Modern .NET EOL → Fallback to .NET 8
    ["net7.0"] = "net8.0",
    ["net6.0"] = "net8.0",
    ["net5.0"] = "net8.0",

    // .NET Core EOL → Fallback to .NET 8
    ["netcoreapp3.1"] = "net8.0",
    ["netcoreapp3.0"] = "net8.0",
    ["netcoreapp2.2"] = "net8.0",
    ["netcoreapp2.1"] = "net8.0",
    ["netcoreapp2.0"] = "net8.0",
};
```

### 3.2 Error Handling for EOL and Invalid Frameworks

When an unsupported framework is specified:

```csharp
public class FrameworkValidationResult
{
    public bool IsValid { get; init; }
    public bool IsSupported { get; init; }
    public bool IsEOL { get; init; }
    public FrameworkInfo? FrameworkInfo { get; init; }
    public string? ErrorMessage { get; init; }
    public string? WarningMessage { get; init; }
    public string? SuggestedFramework { get; init; }
}

// Example error for EOL framework:
{
    IsValid = false,
    IsSupported = false,
    IsEOL = true,
    ErrorMessage = "Unsupported framework: .NET Framework 4.5.2 reached end-of-life on April 26, 2022.",
    SuggestedFramework = "net462",
    WarningMessage = "Consider specifying 'net462' (C# 7.3) or upgrading your project."
}

// Example error for invalid framework:
{
    IsValid = false,
    IsSupported = false,
    IsEOL = false,
    ErrorMessage = "Invalid framework moniker: 'netfx5.0'. Must be valid TFM like 'net8.0', 'net48', 'netstandard2.0'.",
    SuggestedFramework = null
}
```

**Tool Behavior:**
- Tool **rejects** the request with clear error message
- Suggests nearest supported framework when EOL detected
- Does NOT automatically fallback or assume frameworks
- Returns error in standardized format for client handling

### 3.3 New Components

#### Component 1: FrameworkValidator
**Location:** `RefactorCsharpMCP.Core/Analysis/FrameworkValidator.cs`

**Responsibilities:**
- Validate framework moniker format (TFM)
- Detect Microsoft-supported vs EOL frameworks
- Map framework monikers to standardized format
- Provide clear error messages for invalid/unsupported frameworks
- Suggest alternatives for EOL frameworks

**Public API:**
```csharp
public class FrameworkValidator
{
    public FrameworkValidationResult Validate(string targetFramework);
    public bool IsSupportedFramework(string targetFramework);
    public bool IsEOLFramework(string targetFramework);
    public string? GetSuggestedFramework(string eolFramework);
    public string NormalizeMoniker(string targetFramework); // e.g., "v4.8" -> "net48"
}
```

#### Component 2: LanguageVersionMapper
**Location:** `RefactorCsharpMCP.Core/Analysis/LanguageVersionMapper.cs`

**Public API:**
```csharp
public class LanguageVersionMapper
{
    public LanguageVersion GetLanguageVersion(string targetFramework);
    public LanguageVersion GetLanguageVersion(FrameworkInfo frameworkInfo);
    public FrameworkInfo GetFrameworkInfo(string targetFramework);
}
```

#### Component 3: CompilationContextBuilder
**Location:** `RefactorCsharpMCP.Core/Analysis/CompilationContextBuilder.cs`

**Public API:**
```csharp
public class CompilationContextBuilder
{
    public CSharpParseOptions CreateParseOptions(FrameworkInfo frameworkInfo);
    public CSharpCompilation CreateCompilation(
        SyntaxTree syntaxTree,
        FrameworkInfo frameworkInfo);
    public SemanticModel CreateSemanticModel(
        SyntaxTree syntaxTree,
        FrameworkInfo frameworkInfo);
}
```

### 3.4 Updated MCP Tool Signatures

All refactoring tools will be updated to include the required `targetFramework` parameter:

#### Extract Method Tool
```csharp
[McpServerTool]
[Description("Extracts a block of code into a new private method with framework-aware syntax.")]
public Task<object> ExtractMethod(
    [Description("The complete C# source code")]
    string sourceCode,

    [Description("The starting line number (1-based) to extract")]
    int startLine,

    [Description("The ending line number (1-based) to extract")]
    int endLine,

    [Description("The name for the new method")]
    string newMethodName,

    [Description("Target framework moniker (e.g., 'net8.0', 'net48', 'net462', 'netstandard2.0')")]
    string targetFramework)  // REQUIRED
{
    // 1. Validate framework
    var validationResult = _frameworkValidator.Validate(targetFramework);
    if (!validationResult.IsValid || !validationResult.IsSupported)
    {
        return Task.FromResult<object>(new
        {
            success = false,
            error = validationResult.ErrorMessage,
            suggestedFramework = validationResult.SuggestedFramework
        });
    }

    // 2. Get framework info
    var frameworkInfo = _languageMapper.GetFrameworkInfo(targetFramework);

    // 3. Execute refactoring with framework awareness
    var result = _extractor.Execute(sourceCode, startLine, endLine, newMethodName, frameworkInfo);

    // 4. Return result
    return Task.FromResult<object>(new
    {
        success = result.IsSuccess,
        message = result.Message,
        refactoredCode = result.RefactoredCode,
        frameworkInfo = new
        {
            targetFramework = frameworkInfo.TargetFramework,
            languageVersion = frameworkInfo.LanguageVersion.ToString(),
            family = frameworkInfo.Family.ToString()
        }
    });
}
```

#### Constructor Injection Tool
```csharp
[McpServerTool]
[Description("Converts method parameters to constructor-injected fields or properties with framework-aware syntax.")]
public Task<object> ConstructorInjection(
    [Description("The complete C# source code")]
    string sourceCode,

    [Description("The name of the class containing the method")]
    string className,

    [Description("The name of the method with parameters to inject")]
    string methodName,

    [Description("Comma-separated parameter names to inject (e.g., 'logger,config')")]
    string parameterNames,

    [Description("Target framework moniker (e.g., 'net8.0', 'net48', 'net462')")]
    string targetFramework,  // REQUIRED

    [Description("Use properties instead of fields (default: false)")]
    bool useProperties = false)  // Still optional
```

#### All Other Tools
Similar updates to:
- `MakeFieldReadonly` - add required `targetFramework`
- `SafeDelete` - add required `targetFramework`
- `ExtractClass` - add required `targetFramework`

**Parameter Format Examples:**
- Modern .NET: `"net8.0"`, `"net9.0"`
- .NET Framework: `"net48"`, `"net472"`, `"net462"`
- .NET Standard: `"netstandard2.0"`, `"netstandard2.1"`

**Alternative Formats (normalized internally):**
- `"v4.8"` → normalized to `"net48"`
- `.NETFramework,Version=v4.8` → normalized to `"net48"`

### 3.5 Data Models

```csharp
public class FrameworkInfo
{
    public string TargetFramework { get; init; }        // "net8.0", "net462"
    public string TargetFrameworkMoniker { get; init; } // Full TFM
    public FrameworkFamily Family { get; init; }        // Framework, Modern, Standard
    public Version Version { get; init; }               // 4.6.2, 8.0
    public LanguageVersion LanguageVersion { get; init; }
    public bool IsSupported { get; init; }
}

public enum FrameworkFamily
{
    Framework,      // .NET Framework (3.5, 4.6.2-4.8.1)
    Modern,         // .NET 8-9
    Standard        // .NET Standard 2.0-2.1
}

public class FrameworkValidationResult
{
    public bool IsValid { get; init; }           // Valid TFM format
    public bool IsSupported { get; init; }       // Microsoft-supported
    public bool IsEOL { get; init; }             // End-of-life
    public FrameworkInfo? FrameworkInfo { get; init; }
    public string? ErrorMessage { get; init; }
    public string? WarningMessage { get; init; }
    public string? SuggestedFramework { get; init; }  // For EOL frameworks
}
```

## 4. Implementation Plan

### Phase 1: Core Infrastructure (Week 1)
**Deliverables:**
- FrameworkValidator implementation (validation + EOL detection)
- LanguageVersionMapper implementation (mapping table)
- CompilationContextBuilder implementation
- FrameworkInfo and FrameworkValidationResult data models
- Unit tests (25 tests)

**Tests:**
- Framework validation for .NET 8, 9
- Framework validation for .NET Framework 4.6.2-4.8.1
- Framework validation for .NET Standard 2.0, 2.1
- EOL framework rejection (.NET Framework 4.5.2, .NET 6, .NET 7)
- Invalid framework rejection (malformed TFMs)
- Error message generation for unsupported frameworks
- Suggestion generation for EOL frameworks
- Moniker normalization (v4.8 → net48)

### Phase 2: Refactoring Integration (Week 2)
**Deliverables:**
- Update ExtractMethod with required `targetFramework` parameter
- Update ConstructorInjection with required `targetFramework` parameter
- Update MakeFieldReadonly with required `targetFramework` parameter
- Update SafeDelete with required `targetFramework` parameter
- Update ExtractClass with required `targetFramework` parameter
- Integration tests (12 tests)

**Tests:**
- Extract Method with .NET 8 (C# 12) - verify modern syntax allowed
- Extract Method with .NET Framework 4.8 (C# 7.3) - verify no C# 8+ features
- Extract Method with .NET Standard 2.0 (C# 7.3) - verify compatibility
- Constructor Injection rejecting EOL framework
- Make Field Readonly with various frameworks
- All refactorings rejecting invalid frameworks

### Phase 3: MCP Tool Updates (Week 3)
**Deliverables:**
- Add required `targetFramework` parameter to all 5 MCP tools
- Update MCP tool error responses for invalid/EOL frameworks
- Include framework info in success responses
- Update tool descriptions in MCP metadata
- End-to-end MCP tests (8 tests)

**Breaking Change:**
- This is a **breaking API change** - all existing tool calls will fail without `targetFramework`
- Requires version bump to v2.0.0
- Update README with migration guide

### Phase 4: Testing & Documentation (Week 4)
**Deliverables:**
- Complete test suite (45+ new tests total)
- FRAMEWORK-SUPPORT.md documentation
- Updated README.md, EXAMPLES.md
- TROUBLESHOOTING.md updates

### Phase 5: DevTools Validation (Week 5)
**Deliverables:**
- Update all tool invocations to include `targetFramework` parameter
- Test BackupTool refactoring with `targetFramework="net462"`
- Test passgen refactoring with `targetFramework="net8.0"`
- Verify EOL framework rejection for net452
- Real-world examples for all supported frameworks
- Performance benchmarks
- Migration guide for v2.0.0 breaking changes

## 5. Documentation Requirements

### 5.1 New FRAMEWORK-SUPPORT.md

```markdown
# Framework Support Matrix

## Supported .NET Versions (January 2025)

RefactorCsharpMCP supports only Microsoft-supported .NET versions:

### Modern .NET
- ✅ .NET 9 (STS) → C# 13.0 [Supported until Nov 2026]
- ✅ .NET 8 (LTS) → C# 12.0 [Supported until Nov 2026]

### .NET Framework
- ✅ .NET Framework 4.8.1 → C# 7.3
- ✅ .NET Framework 4.8 → C# 7.3
- ✅ .NET Framework 4.7.2 → C# 7.3
- ✅ .NET Framework 4.7.1 → C# 7.3
- ✅ .NET Framework 4.7 → C# 7.3
- ✅ .NET Framework 4.6.2 → C# 7.3
- ✅ .NET Framework 3.5 SP1 → C# 3.0

### .NET Standard
- ✅ .NET Standard 2.1 → C# 8.0
- ✅ .NET Standard 2.0 → C# 7.3

## End-of-Life Frameworks

The following frameworks are **no longer supported by Microsoft**. RefactorCsharpMCP
will **reject** these frameworks with a clear error message:

- ❌ .NET Framework 4.5.2, 4.6, 4.6.1 (EOL: April 2022) → Suggest: net462
- ❌ .NET 7 (EOL: May 2024) → Suggest: net8.0
- ❌ .NET 6 (EOL: November 2024) → Suggest: net8.0
- ❌ .NET 5 (EOL: May 2022) → Suggest: net8.0
- ❌ .NET Core 3.x (EOL: December 2022) → Suggest: net8.0

## What Happens When You Specify an EOL Framework?

When you specify an end-of-life framework, RefactorCsharpMCP will:
1. ❌ **Reject the request** with an error
2. 💡 **Suggest** the nearest supported framework
3. 📝 **Explain** why the framework is not supported

**Example Error Response:**
```json
{
  "success": false,
  "error": "Unsupported framework: .NET Framework 4.5.2 reached end-of-life on April 26, 2022.",
  "suggestedFramework": "net462",
  "message": "Please specify a supported framework. Consider 'net462' (C# 7.3) or upgrading your project."
}
```

## How to Use

All refactoring tools require the `targetFramework` parameter:

```bash
# Example: Extract Method with .NET 8
{
  "sourceCode": "...",
  "startLine": 10,
  "endLine": 20,
  "newMethodName": "ProcessData",
  "targetFramework": "net8.0"
}

# Example: Constructor Injection with .NET Framework 4.8
{
  "sourceCode": "...",
  "className": "MyService",
  "methodName": "Initialize",
  "parameterNames": "logger,config",
  "targetFramework": "net48"
}
```
```

### 5.2 Updated README.md

Add "Supported Frameworks" section listing only Microsoft-supported versions with link to FRAMEWORK-SUPPORT.md.

## 6. Testing Strategy

### 6.1 Test Coverage Matrix

| Test Category | .NET Fx 4.6.2+ | .NET Standard | .NET 8/9 | Total |
|--------------|---------------|---------------|----------|-------|
| Framework Detection | 8 | 2 | 3 | 13 |
| EOL Detection & Fallback | 5 | 0 | 3 | 8 |
| Language Mapping | 3 | 2 | 2 | 7 |
| Extract Method | 3 | 1 | 3 | 7 |
| Other Refactorings | 4 | 1 | 5 | 10 |
| **Total** | **23** | **6** | **16** | **45** |

### 6.2 Critical Test Cases

**TC-1: .NET Framework 4.8 Extract Method**
- Input: Code using C# 7.3 features
- Expected: Successful refactoring with C# 7.3 syntax
- Validation: Compiles with C# 7.3

**TC-2: .NET 8 Extract Method**
- Input: Code using C# 12 features
- Expected: Can use collection expressions, primary constructors
- Validation: Compiles with C# 12

**TC-3: EOL Framework Warning (.NET Framework 4.5.2)**
- Input: BackupTool project file
- Expected: Warning issued, fallback to 4.6.2, refactoring succeeds
- Validation: Warning message present, code uses C# 7.3

**TC-4: .NET Standard 2.0 Compatibility**
- Input: Library targeting netstandard2.0
- Expected: C# 7.3 syntax only
- Validation: No C# 8+ features used

## 7. Success Metrics

### Quantitative
- ✅ Supports 13 framework versions (7 .NET Framework + 2 Modern + 2 Standard + 2 legacy with warnings)
- ✅ 45+ new tests passing
- ✅ 107 existing tests still passing
- ✅ 0 compilation errors for supported frameworks
- ✅ <50ms framework detection overhead

### Qualitative
- ✅ Clear warnings for EOL frameworks
- ✅ DevTools projects handled gracefully (with warnings)
- ✅ Documentation complete

## 8. Acceptance Criteria

1. ✅ All 45+ new tests passing
2. ✅ All 107 existing tests still passing
3. ✅ Supports all Microsoft-supported .NET versions
4. ✅ Rejects EOL frameworks with helpful error messages
5. ✅ All 5 MCP tools require `targetFramework` parameter
6. ✅ Clear error messages for invalid/unsupported frameworks
7. ✅ DevTools projects updated to use new parameter
8. ✅ Documentation complete (FRAMEWORK-SUPPORT.md, migration guide)
9. ✅ Performance <50ms overhead for validation
10. ✅ Version bumped to 2.0.0 (breaking change)

## 9. Breaking Changes (v2.0.0)

### API Changes
- **All MCP tools now require `targetFramework` parameter**
- Existing tool calls without this parameter will fail with clear error
- No automatic detection or fallback behavior

### Migration Path
Users must update all tool calls to include the framework:

**Before (v1.x):**
```json
{
  "sourceCode": "...",
  "startLine": 10,
  "endLine": 20,
  "newMethodName": "ProcessData"
}
```

**After (v2.0):**
```json
{
  "sourceCode": "...",
  "startLine": 10,
  "endLine": 20,
  "newMethodName": "ProcessData",
  "targetFramework": "net8.0"  // REQUIRED
}
```

### Rationale
- Eliminates ambiguity in refactoring behavior
- Makes tool behavior predictable and testable
- Puts framework knowledge responsibility on caller
- Enables accurate, framework-specific code generation

---

**Document Version:** 3.0
**Created:** 2025-10-05
**Updated:** 2025-10-05
**Author:** Seth (with Claude Code)
**Status:** Ready for Implementation
**Scope:** Microsoft-supported .NET versions only (no upgrade tooling)
**Breaking Change:** v2.0.0 - Required `targetFramework` parameter
