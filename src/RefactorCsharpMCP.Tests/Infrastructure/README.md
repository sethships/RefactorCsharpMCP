# Multi-Framework Test Infrastructure

This directory contains the infrastructure for testing refactorings across all 11 supported .NET frameworks.

## Overview

The Multi-Framework Test Infrastructure enables comprehensive testing of C# refactorings across different framework versions, ensuring that generated code is compatible with each framework's C# language version.

### Supported Frameworks (11 Total)

**Modern .NET (2)**
- net9.0 - .NET 9 (C# 13)
- net8.0 - .NET 8 (C# 12)

**.NET Framework (7)**
- net481 - .NET Framework 4.8.1 (C# 7.3)
- net48 - .NET Framework 4.8 (C# 7.3)
- net472 - .NET Framework 4.7.2 (C# 7.3)
- net471 - .NET Framework 4.7.1 (C# 7.3)
- net47 - .NET Framework 4.7 (C# 7.3)
- net462 - .NET Framework 4.6.2 (C# 7.3)
- net35 - .NET Framework 3.5 SP1 (C# 3.0)

**.NET Standard (2)**
- netstandard2.1 - .NET Standard 2.1 (C# 8.0)
- netstandard2.0 - .NET Standard 2.0 (C# 7.3)

## Components

### 1. FrameworkTestFixture

Abstract base class for all framework-aware tests.

**Features:**
- Access to `ReferenceAssemblyResolver` for loading framework-specific assemblies
- Access to `CompilationFactory` for creating Roslyn compilations
- Helper methods for validation and feature detection
- Automatic cleanup of caches after tests

**Usage:**
```csharp
public class MyTests : FrameworkTestFixture
{
    [Theory]
    [FrameworkMatrix]
    public async Task MyTest(string targetFramework)
    {
        var compilation = await CreateTestCompilationAsync(targetFramework, sourceCode);
        CompilationValidator.AssertNoErrors(compilation);
    }
}
```

### 2. FrameworkMatrixAttribute

xUnit `DataAttribute` that generates test cases for all supported frameworks.

**Usage:**
```csharp
// Run test across all 11 frameworks
[Theory]
[FrameworkMatrix]
public async Task TestName(string targetFramework) { }

// Run test only on modern frameworks
[Theory]
[FrameworkMatrix(Filter = FrameworkFamily.Modern)]
public async Task ModernTest(string targetFramework) { }

// Run test only on .NET Framework
[Theory]
[FrameworkMatrix(Filter = FrameworkFamily.Framework)]
public async Task FrameworkTest(string targetFramework) { }
```

### 3. CompilationFactory

Factory for creating framework-aware Roslyn compilations.

**Features:**
- Configures `CSharpParseOptions` with correct `LanguageVersion`
- Sets preprocessor symbols (NET48, NETFRAMEWORK, etc.)
- Configures nullable context options (C# 8.0+)
- Loads framework-specific metadata references

**Usage:**
```csharp
var factory = new CompilationFactory();
var compilation = await factory.CreateCompilationAsync("net8.0", sourceCode);
var semanticModel = await factory.CreateSemanticModelAsync("net48", sourceCode);
```

### 4. FrameworkMappings

Static utilities for framework-specific settings.

**Features:**
- Map framework → `LanguageVersion`
- Map framework → preprocessor symbols
- Map framework → nullable context options
- Feature detection (HasNullableTypes, HasTuples, HasCollectionExpressions)

**Usage:**
```csharp
var langVersion = FrameworkMappings.GetLanguageVersion("net8.0");
var symbols = FrameworkMappings.GetPreprocessorSymbols("net48");
var supportsNullable = FrameworkMappings.HasNullableTypes("net9.0");
```

### 5. CompilationValidator

Utilities for validating compilations in tests.

**Features:**
- Check for compilation errors
- Format diagnostic messages
- Assert no errors with helpful exception messages

**Usage:**
```csharp
CompilationValidator.AssertNoErrors(compilation, "context message");
var errors = CompilationValidator.GetErrors(compilation);
var formatted = CompilationValidator.FormatDiagnostics(errors);
```

### 6. FrameworkSourceBuilder

Fluent builder for creating framework-specific test source code.

**Features:**
- Automatically adjusts syntax based on target framework
- Adds appropriate nullable directives
- Provides pre-built common patterns

**Usage:**
```csharp
var sourceCode = new FrameworkSourceBuilder()
    .ForFramework("net8.0")
    .WithUsing("System")
    .WithClass("MyClass", members: new[] { "public void Method() { }" })
    .Build();

// Or use built-in patterns
var simple = FrameworkSourceBuilder.CreateSimpleClass("net48");
var withFields = FrameworkSourceBuilder.CreateClassWithFields("net35");
```

### 7. SampleCode

Library of pre-built code samples for different scenarios.

**Available Samples:**
- `SimpleClass` - Basic class (all frameworks)
- `ClassWithFields` - Class with fields and properties
- `GenericClass` - Generic types
- `MethodWithLinq` - LINQ queries
- `AsyncMethod` - Async/await
- `NullableTypes` - Nullable reference types (C# 8.0+)
- `TupleReturn` - Tuple types (C# 7.0+)
- `PatternMatching` - Pattern matching (C# 7.0+)
- `CollectionExpressions` - Collection expressions (C# 12+)
- `RecordType` - Record types (C# 9.0+)

**Usage:**
```csharp
var sourceCode = SampleCode.GetSampleForFramework("net8.0", SampleCodeType.Nullable);
```

## Example Tests

See `FrameworkTestFixtureExampleTests.cs` for complete examples.

### Example 1: Test Across All Frameworks

```csharp
[Theory]
[FrameworkMatrix]
public async Task SimpleClass_Compiles_ForAllFrameworks(string targetFramework)
{
    // Arrange
    var sourceCode = FrameworkSourceBuilder.CreateSimpleClass(targetFramework);

    // Act
    var compilation = await CreateTestCompilationAsync(targetFramework, sourceCode);

    // Assert
    CompilationValidator.AssertNoErrors(compilation);
}
```

### Example 2: Test Modern Framework Features

```csharp
[Theory]
[FrameworkMatrix(Filter = FrameworkFamily.Modern)]
public async Task ModernFrameworks_Support_CollectionExpressions(string targetFramework)
{
    // Arrange
    var sourceCode = @"public class Test {
        public List<int> GetNumbers() {
            int[] arr = [1, 2, 3];
            return [.. arr];
        }
    }";

    // Act
    var isValid = await ValidatesSuccessfullyAsync(targetFramework, sourceCode);

    // Assert
    isValid.Should().BeTrue();
}
```

### Example 3: Feature Detection

```csharp
[Theory]
[FrameworkMatrix]
public void FeatureDetection_WorksCorrectly(string targetFramework)
{
    // Check feature support
    var hasNullable = SupportsFeature(targetFramework, FrameworkFeature.NullableTypes);
    var hasTuples = SupportsFeature(targetFramework, FrameworkFeature.Tuples);

    // Verify expectations
    if (targetFramework == "net8.0")
    {
        hasNullable.Should().BeTrue();
        hasTuples.Should().BeTrue();
    }
}
```

## Best Practices

### 1. Use FrameworkTestFixture Base Class
Always inherit from `FrameworkTestFixture` for framework-aware tests. It provides cleanup and helper methods.

### 2. Use FrameworkMatrixAttribute for Matrix Testing
Instead of writing 11 `[InlineData]` attributes, use `[FrameworkMatrix]` to automatically test all frameworks.

### 3. Validate Compilation Success
Always validate that refactored code compiles without errors:
```csharp
CompilationValidator.AssertNoErrors(compilation, contextMessage);
```

### 4. Use Framework-Appropriate Syntax
When creating test source code, ensure it's compatible with the target framework:
```csharp
// Good - uses builder that adjusts syntax
var sourceCode = FrameworkSourceBuilder.CreateSimpleClass(targetFramework);

// Bad - hardcoded modern syntax that fails on old frameworks
var sourceCode = "int[] arr = [1, 2, 3];"; // Fails on net48!
```

### 5. Test Framework-Specific Behavior
Use filters to test behavior specific to framework families:
```csharp
[FrameworkMatrix(Filter = FrameworkFamily.Modern)]  // Only net8.0, net9.0
[FrameworkMatrix(Filter = FrameworkFamily.Framework)] // Only .NET Framework
[FrameworkMatrix(Filter = FrameworkFamily.Standard)]  // Only .NET Standard
```

## Performance Tips

### Parallel Execution
xUnit runs tests in parallel by default. Framework matrix tests execute independently and benefit from parallelization.

### Caching
The `ReferenceAssemblyResolver` caches assemblies in memory and on disk:
- First load: ~500ms (downloads NuGet packages if needed)
- Cached load: <50ms (reads from disk or memory)

### Test Collection
If tests interfere with each other due to caching, use xUnit collections:
```csharp
[Collection("CacheTests")]
public class MyTests : FrameworkTestFixture { }
```

**IMPORTANT**: All classes inheriting from `FrameworkTestFixture` MUST use the
`[Collection("CacheTests")]` attribute to prevent cache concurrency issues. See the
warning in `FrameworkTestFixture.cs` for details.

## Dependencies

This infrastructure depends on:
- **Issue #15**: Reference Assembly Management System (✅ Complete)
- **RefactorCsharpMCP.Core**: FrameworkMoniker, ReferenceAssemblyResolver
- **Microsoft.CodeAnalysis.CSharp 4.14.0**: Roslyn compiler APIs
- **xUnit 2.5.3**: Test framework
- **FluentAssertions 8.7.1**: Assertion library

## Related Documentation

- **Issue #16**: Multi-Framework Test Infrastructure (GitHub)
- **Issue #15**: Reference Assembly Management System (GitHub)
- **docs/SDD-Framework-Version-Awareness.md**: Software Design Document
- **docs/PRD-Framework-Version-Awareness.md**: Product Requirements

## Success Metrics

- ✅ Can compile valid C# code for all 11 frameworks without errors
- ✅ Matrix tests execute efficiently with parallel execution
- ✅ Test fixtures reduce boilerplate by 80%
- ✅ Clear, actionable error messages on compilation failures
- ✅ Comprehensive example tests demonstrate usage
