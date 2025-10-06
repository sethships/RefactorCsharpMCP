# Software Design Document: .NET Framework Version Awareness

**Status:** Draft - Starting Point
**Version:** 0.1
**Last Updated:** 2025-10-05
**Related PRD:** [PRD-Framework-Version-Awareness.md](PRD-Framework-Version-Awareness.md)

> ⚠️ **Note:** This document needs further refinement and is just a starting point. It contains implementation details extracted from the PRD to maintain proper abstraction levels.

## 1. Overview

This Software Design Document provides the technical implementation details for adding .NET framework version awareness to RefactorCsharpMCP. This document contains concrete C# code, class definitions, and implementation specifics that support the high-level requirements defined in the PRD.

## 2. Data Models

### 2.1 Framework Information Model

```csharp
public class FrameworkInfo
{
    public string TargetFramework { get; init; } = string.Empty;
    public LanguageVersion LanguageVersion { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool IsSupported { get; init; }
    public bool IsEOL { get; init; }
    public DateTime? EOLDate { get; init; }
    public string? SupportStatus { get; init; }
}
```

### 2.2 Framework Validation Result

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
```

**Example Error Responses:**

```csharp
// EOL framework error
{
    IsValid = false,
    IsSupported = false,
    IsEOL = true,
    ErrorMessage = "Unsupported framework: .NET Framework 4.5.2 reached end-of-life on April 26, 2022.",
    SuggestedFramework = "net462",
    WarningMessage = "Consider specifying 'net462' (C# 7.3) or upgrading your project."
}

// Invalid framework error
{
    IsValid = false,
    IsSupported = false,
    IsEOL = false,
    ErrorMessage = "Invalid framework moniker: 'netfx5.0'. Must be valid TFM like 'net8.0', 'net48', 'netstandard2.0'.",
    SuggestedFramework = null
}
```

## 3. Framework Mapping Implementation

### 3.1 Supported Framework to Language Version Mapping

```csharp
private static readonly Dictionary<string, LanguageVersion> FrameworkLanguageMap = new()
{
    // Modern .NET (Supported)
    ["net9.0"] = LanguageVersion.CSharp13,      // .NET 9 (STS - Nov 2024 - Nov 2026)
    ["net8.0"] = LanguageVersion.CSharp12,      // .NET 8 (LTS - Nov 2023 - Nov 2026)

    // .NET Framework (Supported)
    ["net481"] = LanguageVersion.CSharp7_3,     // .NET Framework 4.8.1
    ["net48"] = LanguageVersion.CSharp7_3,      // .NET Framework 4.8
    ["net472"] = LanguageVersion.CSharp7_3,     // .NET Framework 4.7.2
    ["net471"] = LanguageVersion.CSharp7_3,     // .NET Framework 4.7.1
    ["net47"] = LanguageVersion.CSharp7_3,      // .NET Framework 4.7
    ["net462"] = LanguageVersion.CSharp7_3,     // .NET Framework 4.6.2
    ["net35"] = LanguageVersion.CSharp3,        // .NET Framework 3.5 SP1

    // .NET Standard (Actively Used)
    ["netstandard2.1"] = LanguageVersion.CSharp8,
    ["netstandard2.0"] = LanguageVersion.CSharp7_3,
};
```

### 3.2 EOL Framework Fallback Mapping

```csharp
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

## 4. Component Architecture

### 4.1 FrameworkValidator

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
    /// <summary>
    /// Validates a target framework moniker and returns detailed validation result.
    /// </summary>
    public FrameworkValidationResult Validate(string targetFramework);

    /// <summary>
    /// Checks if the framework is currently supported by Microsoft.
    /// </summary>
    public bool IsSupportedFramework(string targetFramework);

    /// <summary>
    /// Checks if the framework has reached end-of-life.
    /// </summary>
    public bool IsEOLFramework(string targetFramework);

    /// <summary>
    /// Gets the suggested framework for an EOL framework.
    /// </summary>
    public string? GetSuggestedFramework(string eolFramework);

    /// <summary>
    /// Normalizes framework moniker format (e.g., "v4.8" -> "net48").
    /// </summary>
    public string NormalizeMoniker(string targetFramework);
}
```

### 4.2 LanguageVersionMapper

**Location:** `RefactorCsharpMCP.Core/Analysis/LanguageVersionMapper.cs`

**Responsibilities:**
- Map framework monikers to C# language versions
- Provide framework metadata
- Handle version-specific language features

**Public API:**
```csharp
public class LanguageVersionMapper
{
    /// <summary>
    /// Gets the C# language version for a target framework.
    /// </summary>
    public LanguageVersion GetLanguageVersion(string targetFramework);

    /// <summary>
    /// Gets the C# language version from framework info.
    /// </summary>
    public LanguageVersion GetLanguageVersion(FrameworkInfo frameworkInfo);

    /// <summary>
    /// Gets complete framework information for a target framework.
    /// </summary>
    public FrameworkInfo GetFrameworkInfo(string targetFramework);
}
```

### 4.3 CompilationContextBuilder

**Location:** `RefactorCsharpMCP.Core/Analysis/CompilationContextBuilder.cs`

**Responsibilities:**
- Create framework-aware parse options
- Build Roslyn compilation with correct references
- Configure semantic model for specific framework

**Public API:**
```csharp
public class CompilationContextBuilder
{
    /// <summary>
    /// Creates parse options configured for the target framework.
    /// </summary>
    public CSharpParseOptions CreateParseOptions(FrameworkInfo frameworkInfo);

    /// <summary>
    /// Creates a compilation context for the target framework.
    /// </summary>
    public CSharpCompilation CreateCompilation(
        SyntaxTree syntaxTree,
        FrameworkInfo frameworkInfo);

    /// <summary>
    /// Creates a semantic model for the target framework.
    /// </summary>
    public SemanticModel CreateSemanticModel(
        SyntaxTree syntaxTree,
        FrameworkInfo frameworkInfo);
}
```

## 5. MCP Tool Signature Updates

### 5.1 Extract Method Tool

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
    string targetFramework)
```

### 5.2 Constructor Injection Tool

```csharp
[McpServerTool]
[Description("Converts method parameters to constructor-injected fields or properties.")]
public Task<object> ConstructorInjection(
    [Description("The complete C# source code")]
    string sourceCode,

    [Description("The name of the class containing the method")]
    string className,

    [Description("The name of the method with parameters to inject")]
    string methodName,

    [Description("Comma-separated parameter names to inject (e.g., 'logger,config')")]
    string parameterNames,

    [Description("Use properties instead of fields (default: false)")]
    bool useProperties = false,

    [Description("Target framework moniker (e.g., 'net8.0', 'net48', 'net462', 'netstandard2.0')")]
    string targetFramework = "")  // Required in v2.0.0
```

### 5.3 Make Field Readonly Tool

```csharp
[McpServerTool]
[Description("Makes a field readonly if it is only assigned in constructors.")]
public Task<object> MakeFieldReadonly(
    [Description("The complete C# source code")]
    string sourceCode,

    [Description("The name of the class containing the field")]
    string className,

    [Description("The name of the field to make readonly")]
    string fieldName,

    [Description("Target framework moniker (e.g., 'net8.0', 'net48', 'net462', 'netstandard2.0')")]
    string targetFramework)
```

### 5.4 Safe Delete Method Tool

```csharp
[McpServerTool]
[Description("Safely deletes a method after verifying it has no references within the same file.")]
public Task<object> SafeDeleteMethod(
    [Description("The complete C# source code")]
    string sourceCode,

    [Description("The name of the class containing the method")]
    string className,

    [Description("The name of the method to delete")]
    string methodName,

    [Description("Target framework moniker (e.g., 'net8.0', 'net48', 'net462', 'netstandard2.0')")]
    string targetFramework)
```

### 5.5 Extract Class Tool

```csharp
[McpServerTool]
[Description("Extracts fields and methods into a new class with composition pattern.")]
public Task<object> ExtractClass(
    [Description("The complete C# source code")]
    string sourceCode,

    [Description("The name of the source class")]
    string className,

    [Description("The name of the new class to create")]
    string newClassName,

    [Description("Comma or semicolon-separated field names to extract")]
    string fieldNames,

    [Description("Comma or semicolon-separated method names to extract (optional)")]
    string? methodNames = null,

    [Description("Target framework moniker (e.g., 'net8.0', 'net48', 'net462', 'netstandard2.0')")]
    string targetFramework = "")  // Required in v2.0.0
```

## 6. Implementation Details

### 6.1 ExtractMethod Refactoring Changes

**Current Implementation (line 48):**
```csharp
var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
```

**Updated Implementation:**
```csharp
// Validate framework
var validationResult = _frameworkValidator.Validate(targetFramework);
if (!validationResult.IsValid)
{
    return RefactoringResult.Failure(validationResult.ErrorMessage!);
}

// Get framework info and language version
var frameworkInfo = _languageVersionMapper.GetFrameworkInfo(targetFramework);
var parseOptions = _compilationContextBuilder.CreateParseOptions(frameworkInfo);

// Parse with framework-aware options
var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions);
```

**Current Compilation (lines 75-81):**
```csharp
var compilation = CSharpCompilation.Create("temp")
    .AddReferences(
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
    )
    .AddSyntaxTrees(syntaxTree);
```

**Updated Compilation:**
```csharp
var compilation = _compilationContextBuilder.CreateCompilation(syntaxTree, frameworkInfo);
```

### 6.2 Parse Options Configuration

```csharp
public CSharpParseOptions CreateParseOptions(FrameworkInfo frameworkInfo)
{
    return new CSharpParseOptions(
        languageVersion: frameworkInfo.LanguageVersion,
        documentationMode: DocumentationMode.Parse,
        kind: SourceCodeKind.Regular
    );
}
```

### 6.3 Framework-Specific Compilation References

```csharp
public CSharpCompilation CreateCompilation(SyntaxTree syntaxTree, FrameworkInfo frameworkInfo)
{
    var references = GetFrameworkReferences(frameworkInfo);

    return CSharpCompilation.Create("RefactoringCompilation")
        .AddReferences(references)
        .AddSyntaxTrees(syntaxTree)
        .WithOptions(new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: false
        ));
}

private IEnumerable<MetadataReference> GetFrameworkReferences(FrameworkInfo frameworkInfo)
{
    // Base references for all frameworks
    var references = new List<MetadataReference>
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
    };

    // Add framework-specific references
    if (frameworkInfo.TargetFramework.StartsWith("net8") ||
        frameworkInfo.TargetFramework.StartsWith("net9"))
    {
        // .NET 8/9 specific references
        // Add System.Runtime, System.Collections, etc.
    }
    else if (frameworkInfo.TargetFramework.StartsWith("net4"))
    {
        // .NET Framework specific references
        // Add mscorlib, System.Core, etc.
    }

    return references;
}
```

## 7. Error Handling

### 7.1 Tool Behavior on Invalid Framework

**Reject Request Immediately:**
```csharp
public async Task<object> ExtractMethod(
    string sourceCode,
    int startLine,
    int endLine,
    string newMethodName,
    string targetFramework)
{
    // Step 1: Validate framework FIRST
    var validationResult = _frameworkValidator.Validate(targetFramework);

    if (!validationResult.IsValid)
    {
        return new
        {
            success = false,
            error = validationResult.ErrorMessage,
            suggestion = validationResult.SuggestedFramework,
            warning = validationResult.WarningMessage
        };
    }

    // Step 2: Proceed with refactoring
    var result = _extractMethod.Execute(
        sourceCode,
        startLine,
        endLine,
        newMethodName,
        validationResult.FrameworkInfo!);

    return FormatResult(result);
}
```

### 7.2 Error Message Format

```csharp
// EOL Framework Error
{
    "success": false,
    "error": "Unsupported framework: .NET Framework 4.5.2 reached end-of-life on April 26, 2022.",
    "suggestion": "net462",
    "warning": "Consider specifying 'net462' (C# 7.3) or upgrading your project."
}

// Invalid Framework Error
{
    "success": false,
    "error": "Invalid framework moniker: 'netfx5.0'. Must be valid TFM like 'net8.0', 'net48', 'netstandard2.0'.",
    "suggestion": null,
    "warning": null
}

// Successful Refactoring
{
    "success": true,
    "message": "Extracted method 'ProcessData' from lines 10-20.",
    "refactoredCode": "...",
    "frameworkInfo": {
        "targetFramework": "net8.0",
        "languageVersion": "CSharp12",
        "displayName": ".NET 8"
    }
}
```

## 8. Testing Approach

### 8.1 Unit Tests for FrameworkValidator

```csharp
[Fact]
public void Validate_SupportedFramework_ReturnsValid()
{
    var validator = new FrameworkValidator();
    var result = validator.Validate("net8.0");
    
    Assert.True(result.IsValid);
    Assert.True(result.IsSupported);
    Assert.False(result.IsEOL);
}

[Fact]
public void Validate_EOLFramework_ReturnsInvalidWithSuggestion()
{
    var validator = new FrameworkValidator();
    var result = validator.Validate("net452");
    
    Assert.False(result.IsValid);
    Assert.False(result.IsSupported);
    Assert.True(result.IsEOL);
    Assert.Equal("net462", result.SuggestedFramework);
}

[Fact]
public void Validate_InvalidFormat_ReturnsInvalid()
{
    var validator = new FrameworkValidator();
    var result = validator.Validate("netfx5.0");
    
    Assert.False(result.IsValid);
    Assert.False(result.IsSupported);
    Assert.Contains("Invalid framework moniker", result.ErrorMessage);
}
```

### 8.2 Integration Tests for ExtractMethod

```csharp
[Fact]
public void ExtractMethod_WithNet8_UsesModernSyntax()
{
    var sourceCode = @"public class Test {
        public void Method() {
            var x = 1;
            var y = 2;
            var z = x + y;
        }
    }";
    
    var tool = new ExtractMethodTool();
    var result = await tool.ExtractMethod(
        sourceCode, 3, 5, "Calculate", "net8.0");
    
    Assert.True(result.success);
    Assert.Contains("net8.0", result.frameworkInfo.targetFramework);
}

[Fact]
public void ExtractMethod_WithEOLFramework_RejectsRequest()
{
    var tool = new ExtractMethodTool();
    var result = await tool.ExtractMethod(
        "...", 1, 2, "Test", "net452");
    
    Assert.False(result.success);
    Assert.Contains("end-of-life", result.error);
    Assert.Equal("net462", result.suggestion);
}
```

---

**Next Steps:**
1. Implement FrameworkValidator component
2. Implement LanguageVersionMapper component
3. Implement CompilationContextBuilder component
4. Update ExtractMethod refactoring
5. Update all other MCP tools
6. Add comprehensive test suite
7. Update documentation
