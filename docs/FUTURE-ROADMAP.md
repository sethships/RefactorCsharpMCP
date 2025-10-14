# RefactorCsharpMCP - Future Roadmap (V2.0+)

**Document Version:** 1.0.0
**Last Updated:** 2025-10-10
**Status:** Planning / Future Enhancements

---

## Overview

This document outlines potential future enhancements for RefactorCsharpMCP beyond the V1.0 release. These features are **explicitly non-goals for V1** to maintain focus on delivering core refactoring capabilities with framework awareness.

V1 focuses on:
- 10 core refactorings (single-file scope)
- Framework awareness (13 .NET versions)
- MCP integration for AI agents
- 8-9 week delivery timeline

This roadmap explores opportunities for V2.0 and beyond, prioritized by user value and architectural feasibility.

---

## V2.0: Cross-File Refactoring & Diagnostic Integration

**Timeline:** 8-10 weeks after V1.0 release
**Theme:** Expand refactoring scope beyond single files

### 2.1 Cross-File Refactoring Capabilities

**Motivation:**
- V1 refactorings are limited to single-file scope
- Real-world codebases require workspace-wide analysis
- Users need to refactor symbols used across multiple files

**Planned Refactorings:**

#### 2.1.1 Rename Symbol (Cross-File)
**Scope:** Rename classes, methods, properties across entire workspace

**User Story (Sarah - Full-Stack Developer):**
> "I renamed `UserRepository` to `UserDataStore` but it's used in 47 files. I need the tool to update all references."

**Technical Requirements:**
- Workspace symbol resolution (multi-file semantic model)
- Reference finding across solution
- Batch file updates with transactional semantics
- Rollback capability on errors

**Effort Estimate:** 5-7 days

#### 2.1.2 Move Type to New File
**Scope:** Extract class/interface/enum to separate file with correct namespace

**User Story (Mike - Legacy Maintainer):**
> "I have a 5000-line file with 12 classes. I want to move each class to its own file."

**Technical Requirements:**
- Namespace inference from folder structure
- File naming conventions
- Using directive optimization
- Reference updates in other files

**Effort Estimate:** 4-6 days

#### 2.1.3 Safe Delete (Cross-File)
**Scope:** Delete types/members with workspace-wide reference checking

**Enhancement over V1:**
- V1 Safe Delete checks single file only
- V2 checks entire workspace for references
- Reports all usage locations before deletion

**Effort Estimate:** 3-4 days (builds on V1 implementation)

#### 2.1.4 Extract Interface (Cross-File)
**Scope:** Extract interface from class and update all references to use interface

**User Story (Sarah - Full-Stack Developer):**
> "I want to extract `IUserRepository` from `UserRepository` and update all constructor parameters to use the interface."

**Technical Requirements:**
- Interface member selection
- Reference type updates (class → interface)
- Dependency injection pattern application
- Multi-file coordination

**Effort Estimate:** 6-8 days

### 2.2 Diagnostic Discovery & Integration

**Motivation:**
- Linters identify code smells but don't fix them
- Natural pipeline: Detect issue → Suggest refactoring → Execute fix
- Roslyn already provides comprehensive diagnostics (CA/IDE rules)

**Approach:** Expose existing Roslyn diagnostics, don't reinvent the wheel

#### 2.2.1 `analyze_code` Tool

**MCP Tool API:**
```json
{
  "name": "analyze_code",
  "description": "Analyze C# code for compiler warnings, style violations, and code quality issues using Roslyn diagnostics",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": {
        "type": "string",
        "description": "C# source code to analyze"
      },
      "targetFramework": {
        "type": "string",
        "description": "Target framework moniker (e.g., net8.0, net48)"
      },
      "severity": {
        "type": "string",
        "enum": ["error", "warning", "info", "all"],
        "description": "Minimum severity level to report (default: warning)"
      },
      "categories": {
        "type": "array",
        "items": {
          "type": "string",
          "enum": ["style", "quality", "performance", "security", "maintainability"]
        },
        "description": "Diagnostic categories to include (default: all)"
      }
    },
    "required": ["sourceCode", "targetFramework"]
  }
}
```

**Output Schema:**
```json
{
  "success": true,
  "diagnostics": [
    {
      "id": "IDE0005",
      "severity": "info",
      "message": "Using directive is unnecessary",
      "location": {
        "line": 3,
        "column": 1,
        "span": { "start": 45, "length": 23 }
      },
      "category": "style",
      "applicableRefactorings": ["remove_unused_usings"]
    },
    {
      "id": "IDE0044",
      "severity": "info",
      "message": "Add readonly modifier",
      "location": {
        "line": 12,
        "column": 9,
        "span": { "start": 234, "length": 15 }
      },
      "category": "style",
      "applicableRefactorings": ["make_field_readonly"]
    }
  ],
  "summary": {
    "totalDiagnostics": 2,
    "errors": 0,
    "warnings": 0,
    "info": 2
  }
}
```

**Key Features:**
- Exposes Roslyn's built-in CA/IDE analyzers
- Maps diagnostics → applicable refactorings
- Framework-aware analysis (uses targetFramework)
- No custom rule engine (uses existing Roslyn infrastructure)

**User Story (AI Agent - Claude Code):**
> "User asked me to 'clean up this code'. I'll run `analyze_code` to find issues, then proactively suggest: 'I found 5 unused usings and 3 fields that could be readonly. Shall I fix them?'"

**Effort Estimate:** 4-5 days

#### 2.2.2 `fix_diagnostic` Tool

**MCP Tool API:**
```json
{
  "name": "fix_diagnostic",
  "description": "Automatically fix a specific Roslyn diagnostic by applying the appropriate refactoring",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": {
        "type": "string",
        "description": "C# source code containing the diagnostic"
      },
      "targetFramework": {
        "type": "string",
        "description": "Target framework moniker (e.g., net8.0, net48)"
      },
      "diagnosticId": {
        "type": "string",
        "description": "Roslyn diagnostic ID to fix (e.g., IDE0005, IDE0044)"
      },
      "location": {
        "type": "object",
        "properties": {
          "line": { "type": "number" },
          "column": { "type": "number" }
        },
        "description": "Location of the diagnostic in the source code"
      }
    },
    "required": ["sourceCode", "targetFramework", "diagnosticId", "location"]
  }
}
```

**Diagnostic → Refactoring Mapping:**

| Diagnostic ID | Description | Mapped Refactoring |
|---------------|-------------|-------------------|
| IDE0005 | Using directive is unnecessary | `remove_unused_usings` |
| CS8019 | Unnecessary using directive | `remove_unused_usings` |
| IDE0044 | Add readonly modifier | `make_field_readonly` |
| IDE0007 | Use 'var' instead of explicit type | `inline_variable` (future enhancement) |
| IDE0058 | Expression value is never used | Safe delete or extract method |
| CA1822 | Mark members as static | Future refactoring |
| CA1802 | Use literals where appropriate | Inline variable |

**User Story (Sarah - Full-Stack Developer):**
> "My CI pipeline failed with 'IDE0044: Add readonly modifier' on line 45. I'll ask Claude Code to `fix_diagnostic(code, 'IDE0044', line=45)` instead of doing it manually."

**Effort Estimate:** 3-4 days (builds on `analyze_code`)

#### 2.2.3 Batch Diagnostic Fixing

**Enhancement:** Apply multiple diagnostic fixes in one operation

**Use Case:**
- User has 50 unused usings across a file
- Instead of 50 individual tool calls, one batch operation

**Effort Estimate:** 2-3 days

**V2.0 Total Effort:** 27-38 days (5-7 weeks)

---

## V2.5: Linter Support & Advanced Diagnostics

**Timeline:** 6-8 weeks after V2.0 release
**Theme:** Custom analysis rules and framework-specific linting

### 2.5.1 Framework-Specific Diagnostics

**Motivation:**
- Prevent C# 8+ features in .NET Framework 4.8 code (Mike's critical requirement)
- Warn about obsolete APIs in specific framework versions
- Enforce framework-appropriate patterns

**Custom Diagnostic Rules:**

#### Rule: RCMCP0001 - Unsupported Language Feature
**Severity:** Error
**Description:** Detects C# language features unavailable in target framework

**Examples:**
```csharp
// Targeting net48 (C# 7.3)
var items = new List<string>(); // ✅ OK
var items = ["a", "b", "c"];    // ❌ RCMCP0001: Collection expressions require C# 12 (net48 uses C# 7.3)

// Targeting net462 (C# 7.3)
var (name, age) = GetData();    // ✅ OK (tuples added in C# 7.0)
public record User(string Name); // ❌ RCMCP0001: Records require C# 9 (net462 uses C# 7.3)
```

**User Story (Mike - Legacy Maintainer):**
> "I need the tool to prevent my team from accidentally using modern C# features in our .NET Framework 4.6.2 codebase. Compilation errors are too late—I want IDE warnings."

#### Rule: RCMCP0002 - Obsolete Framework API
**Severity:** Warning
**Description:** Detects obsolete or deprecated APIs for target framework

**Examples:**
```csharp
// Targeting net8.0
var sha1 = new SHA1CryptoServiceProvider(); // ⚠️ RCMCP0002: SHA1CryptoServiceProvider is obsolete in .NET 8

// Targeting net48
var handler = new HttpClientHandler();
handler.UseCookies = true; // ✅ OK in Framework 4.8
```

#### Rule: RCMCP0003 - Framework-Inappropriate Pattern
**Severity:** Info
**Description:** Suggests framework-appropriate alternatives

**Examples:**
```csharp
// Targeting net48 (no IAsyncEnumerable<T>)
public IAsyncEnumerable<string> GetItemsAsync() // ℹ️ RCMCP0003: IAsyncEnumerable unavailable in net48, use IEnumerable<Task<T>>

// Targeting net8.0
public DataTable GetUsers() // ℹ️ RCMCP0003: Consider modern collection types (List<T>, IEnumerable<T>) instead of DataTable
```

**Effort Estimate:** 6-8 days

### 2.5.2 Architectural Rule Engine

**Motivation:**
- Enforce layering rules (no UI code calling DB directly)
- Dependency direction validation (Domain → Infrastructure ❌)
- Namespace conventions

**Examples:**

#### Rule: RCMCP1001 - Layering Violation
```csharp
// In MyApp.UI project
using MyApp.DataAccess; // ❌ RCMCP1001: UI layer cannot reference DataAccess layer directly

public class UserController {
    private readonly UserRepository _repo; // ❌ Use IUserRepository via dependency injection
}
```

#### Rule: RCMCP1002 - Namespace Mismatch
```csharp
// File: src/MyApp.Domain/Services/UserService.cs
namespace MyApp.Business.Logic // ❌ RCMCP1002: Namespace should match folder structure: MyApp.Domain.Services
{
    public class UserService { }
}
```

**Configuration:** `.refactorconfig` file
```json
{
  "architectureRules": {
    "layering": {
      "enabled": true,
      "layers": [
        { "name": "UI", "canReference": ["Application"] },
        { "name": "Application", "canReference": ["Domain"] },
        { "name": "Domain", "canReference": [] },
        { "name": "Infrastructure", "canReference": ["Domain"] }
      ]
    },
    "namespaceConventions": {
      "enabled": true,
      "enforceFolderStructure": true
    }
  }
}
```

**Effort Estimate:** 8-10 days

### 2.5.3 .editorconfig Integration

**Feature:** Respect existing .editorconfig rules for style analysis

**Examples:**
```ini
# .editorconfig
[*.cs]
dotnet_style_prefer_auto_properties = true:warning
csharp_prefer_braces = true:warning
```

**Integration:**
- Load .editorconfig from workspace
- Apply configured rules in `analyze_code`
- Respect severity levels (silent, suggestion, warning, error)

**Effort Estimate:** 4-5 days

### 2.5.4 Reference Assembly Cache Eviction Policy

**Motivation:**
- V1.0 implements three-tier caching (Memory → Disk → NuGet) for reference assemblies
- Cache can grow to ~550MB (50MB per framework × 11 frameworks)
- No automatic eviction policy in V1 - users must manually manage cache
- Long-running servers or users working with multiple frameworks may accumulate cache bloat

**Current V1.0 Behavior:**
- **Cache Location:** `%USERPROFILE%/.refactor-csharp-mcp/reference-assemblies/`
- **Cache Persistence:** Cache persists indefinitely across server restarts
- **Manual Management:** Users must manually delete cache directories
- **No Size Limits:** Cache can grow without bounds if users work with all frameworks

**User Story (Sarah - Full-Stack Developer):**
> "I've been using RefactorCsharpMCP for 6 months across 15 different projects. My cache directory is now 2.3GB and I'm not sure which frameworks I'm still using. I'd like automatic cleanup of unused frameworks."

**Proposed V2.5.4 Feature: LRU Cache Eviction**

#### Implementation Strategy

**LRU (Least Recently Used) Policy:**
- Track last access time for each framework cache
- When total cache size exceeds threshold, evict oldest unused frameworks
- Configurable cache size limit (default: 1GB, ~18 frameworks worth)

**Cache Manifest Enhancement:**
```json
{
  "frameworks": {
    "net8.0": {
      "targetFramework": "net8.0",
      "cachedAt": "2025-10-01T10:30:00Z",
      "lastAccessedAt": "2025-10-10T14:22:00Z",
      "accessCount": 47,
      "assemblyCou": 142,
      "sizeBytes": 52428800,
      "nuGetPackageVersion": "1.0.3"
    },
    "net481": {
      "targetFramework": "net481",
      "cachedAt": "2025-09-15T08:15:00Z",
      "lastAccessedAt": "2025-09-20T11:00:00Z",
      "accessCount": 3,
      "assemblyCount": 138,
      "sizeBytes": 48234567,
      "nuGetPackageVersion": null
    }
  },
  "cachePolicy": {
    "maxSizeBytes": 1073741824,
    "evictionStrategy": "lru",
    "retentionDays": 30
  }
}
```

**Eviction Algorithm:**
```csharp
public void EnforceCachePolicy()
{
    var manifest = LoadManifest();
    var policy = manifest.CachePolicy;
    var totalSize = CalculateTotalCacheSize();

    if (totalSize > policy.MaxSizeBytes)
    {
        var frameworksByLastAccess = manifest.Frameworks
            .OrderBy(f => f.Value.LastAccessedAt)
            .ToList();

        foreach (var framework in frameworksByLastAccess)
        {
            // Never evict recently accessed frameworks (within 7 days)
            if ((DateTime.UtcNow - framework.Value.LastAccessedAt).TotalDays < 7)
                continue;

            // Evict oldest framework
            ClearCache(framework.Key);
            totalSize -= framework.Value.SizeBytes;

            _logger?.LogInformation(
                "Evicted framework {Framework} (last accessed {Days} days ago)",
                framework.Key,
                (DateTime.UtcNow - framework.Value.LastAccessedAt).TotalDays);

            if (totalSize <= policy.MaxSizeBytes * 0.8) // Leave 20% buffer
                break;
        }
    }
}
```

**Configuration Options:**

Users can configure cache policy via `.refactorconfig` or environment variables:

```json
{
  "referenceAssemblyCache": {
    "maxSizeGB": 1.0,
    "evictionStrategy": "lru",
    "retentionDays": 30,
    "autoEviction": true,
    "protectedFrameworks": ["net8.0", "net9.0"]
  }
}
```

**Environment Variables:**
```bash
REFACTOR_CSHARP_CACHE_MAX_SIZE_GB=2.0
REFACTOR_CSHARP_CACHE_AUTO_EVICTION=true
REFACTOR_CSHARP_CACHE_RETENTION_DAYS=60
```

#### MCP Tool Enhancement

**New Tool: `get_cache_statistics`**
```json
{
  "name": "get_cache_statistics",
  "description": "Get reference assembly cache statistics including size, framework details, and eviction recommendations",
  "inputSchema": {
    "type": "object",
    "properties": {
      "includeRecommendations": {
        "type": "boolean",
        "description": "Include eviction recommendations based on LRU policy"
      }
    }
  }
}
```

**Output Example:**
```json
{
  "totalSizeBytes": 723517440,
  "totalSizeFormatted": "690 MB",
  "totalFrameworks": 14,
  "cacheRoot": "C:\\Users\\sarah\\.refactor-csharp-mcp\\reference-assemblies",
  "frameworks": {
    "net8.0": {
      "sizeBytes": 52428800,
      "assemblies": 142,
      "lastAccessed": "2025-10-10T14:22:00Z",
      "daysSinceAccess": 0,
      "accessCount": 47
    },
    "net481": {
      "sizeBytes": 48234567,
      "assemblies": 138,
      "lastAccessed": "2025-09-20T11:00:00Z",
      "daysSinceAccess": 20,
      "accessCount": 3
    }
  },
  "recommendations": {
    "evictionCandidates": ["net461", "net35", "netstandard2.0"],
    "potentialSavingsBytes": 145600000,
    "potentialSavingsFormatted": "138 MB",
    "reason": "These frameworks haven't been accessed in 30+ days"
  },
  "cachePolicy": {
    "maxSizeBytes": 1073741824,
    "autoEviction": true,
    "retentionDays": 30
  }
}
```

**New Tool: `clear_cache`**
```json
{
  "name": "clear_cache",
  "description": "Clear reference assembly cache for specific frameworks or all frameworks",
  "inputSchema": {
    "type": "object",
    "properties": {
      "frameworks": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Framework monikers to clear. Omit to clear all frameworks."
      },
      "olderThanDays": {
        "type": "number",
        "description": "Only clear frameworks not accessed in this many days"
      }
    }
  }
}
```

#### User Experience Enhancements

**Proactive Warnings:**
```
[ReferenceAssemblyResolver] Cache size 1.2GB exceeds limit (1GB).
Evicting 3 unused frameworks: net461, net35, netstandard2.0
Freed 138MB. Cache now 1.06GB.
```

**User Control:**
```bash
# AI Agent conversation
User: "My cache is getting large"
Agent: *calls get_cache_statistics*
Agent: "Your cache is 1.4GB with 18 frameworks. I can free 320MB by removing 5 frameworks you haven't used in 45+ days: net461, net35, net462, net471, netstandard2.0. Shall I?"
User: "Yes, but keep net462"
Agent: *calls clear_cache with specific frameworks*
Agent: "Done! Cleared 4 frameworks and freed 256MB. Cache now 1.14GB."
```

#### Performance Considerations

**Cache Access Tracking Overhead:**
- Update `lastAccessedAt` timestamp: <1ms per access
- Update manifest file: Async, non-blocking
- LRU sorting: O(N log N) where N = 11-20 frameworks (negligible)

**Eviction Performance:**
- Check policy on every cache access: <1ms (in-memory check)
- Eviction execution: Only when threshold exceeded
- Background eviction: Run async during idle time

**Effort Estimate:** 6-8 days
- LRU tracking infrastructure: 2 days
- Manifest enhancement: 1 day
- Eviction algorithm: 2 days
- MCP tool integration: 1-2 days
- Testing and edge cases: 1-2 days

#### Alternative Approaches

**Option A: Time-Based Eviction (Simpler)**
- Automatically delete caches older than N days (e.g., 90 days)
- No size tracking, just time-based
- **Effort:** 3-4 days
- **Trade-off:** Less intelligent, may evict frequently used frameworks

**Option B: User-Prompted Eviction (Minimal)**
- No automatic eviction
- Log warnings when cache exceeds threshold
- Provide MCP tool for manual cleanup
- **Effort:** 2-3 days (V1 approach with better tooling)

**Option C: Hybrid (Recommended for V2.5.4)**
- Implement time-based eviction first (3-4 days)
- Add optional LRU policy (additional 4-5 days)
- Users can choose via configuration
- **Total Effort:** 7-9 days

**Architect Recommendation:**
- **V2.5.4: Implement Option C (Hybrid)** - provides both simplicity and power
- **Default policy:** Time-based (30 days) with optional LRU
- **Future V3.0:** Add workspace-aware eviction (only evict frameworks not in active solution)

**V2.5 Total Effort:** 25-32 days (4-6 weeks) with cache eviction policy included

---

## V3.0: Workspace Intelligence & AI-Powered Suggestions

**Timeline:** 8-12 weeks after V2.5 release
**Theme:** Proactive refactoring suggestions and large-scale transformations

### 3.1 Smart Refactoring Suggestions

**Motivation:**
- AI agents can proactively suggest refactorings based on code analysis
- "Hey, I noticed this class has 15 methods—want me to extract some?"

#### 3.1.1 `suggest_refactorings` Tool

**MCP Tool API:**
```json
{
  "name": "suggest_refactorings",
  "description": "Analyze code and suggest applicable refactorings based on code smells, patterns, and best practices",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": { "type": "string" },
      "targetFramework": { "type": "string" },
      "context": {
        "type": "string",
        "enum": ["maintenance", "feature-development", "code-review", "modernization"],
        "description": "Development context to prioritize suggestions"
      }
    },
    "required": ["sourceCode", "targetFramework"]
  }
}
```

**Output Example:**
```json
{
  "suggestions": [
    {
      "refactoring": "extract_method",
      "priority": "high",
      "reason": "Method 'ProcessOrder' has 87 lines (threshold: 50)",
      "location": { "line": 45, "column": 5 },
      "estimatedImpact": "Improves testability and readability",
      "codeSmell": "long-method"
    },
    {
      "refactoring": "extract_class",
      "priority": "medium",
      "reason": "Class 'OrderService' has 23 methods and 1247 lines",
      "location": { "line": 1, "column": 1 },
      "estimatedImpact": "Reduces god class anti-pattern",
      "codeSmell": "god-class"
    }
  ]
}
```

**Code Smell Detection:**
- Long methods (>50 lines)
- God classes (>15 methods, >500 lines)
- Feature envy (method uses another class's data extensively)
- Duplicate code (similar logic repeated)
- Deep nesting (>4 levels of indentation)

**Effort Estimate:** 10-12 days

### 3.2 Bulk Refactoring Operations

**Motivation:**
- Apply same refactoring to multiple locations
- Example: Convert all 47 constructors in a project to use dependency injection

#### 3.2.1 `bulk_refactor` Tool

**Use Cases:**
- Apply "Constructor Injection" to all controllers in a project
- Make all applicable fields readonly across solution
- Remove all unused usings in workspace

**Effort Estimate:** 6-8 days

### 3.3 Migration Assistants

**Motivation:**
- Framework migration scenarios (.NET Framework → .NET 8)
- Pattern migration (Repository pattern → CQRS)

#### 3.3.1 Framework Migration Assistant

**User Story (Mike - Legacy Maintainer):**
> "I need to migrate our .NET Framework 4.8 ERP system to .NET 8. Show me which code patterns need updating and help me refactor them."

**Features:**
- Identify incompatible APIs (WebForms, WCF, Remoting)
- Suggest modern alternatives (ASP.NET Core, gRPC, HTTP)
- Batch refactoring for migration patterns

**Effort Estimate:** 15-20 days (complex feature)

#### 3.3.2 Pattern Migration

**Examples:**
- Service Locator → Dependency Injection
- DataTable → Entity Framework / Dapper
- Manual null checks → Nullable reference types

**Effort Estimate:** 12-15 days per pattern

**V3.0 Total Effort:** 43-55 days (8-11 weeks)

---

## Deferred Implementations: Collection Expression Converter

**Last Updated:** 2025-10-13
**Status:** Intentionally Deferred
**Target:** V2.5 or later (demand-driven)

### Overview

The CollectionExpressionConverter exists as an architectural demonstration but is NOT fully implemented.
This is an **intentional decision**, not a technical limitation.

**IMPORTANT:** CollectionExpressionSyntax API is **fully available** in Roslyn 4.14.0 (introduced in 4.7.0).
The documentation claiming "API not yet available" was outdated and has been corrected.

### Current Implementation Status

**Location:** `src/RefactorCsharpMCP.Core/SyntaxConversion/CollectionExpressionConverter.cs`

**What Exists:**
- ✅ Converter class inheriting from SyntaxConverterBase
- ✅ Correct language version properties (MinimumSourceLanguageVersion = C# 12, MaximumTargetLanguageVersion = C# 11)
- ✅ Framework compatibility checking (CanConvert method)
- ✅ Basic test coverage (7 tests verifying properties and framework detection)

**What's Missing:**
- ❌ Override of `VisitCollectionExpression` method
- ❌ Type inference for empty collections (`[]` → `Array.Empty<T>()`)
- ❌ Spread element conversion (`[..arr]` → `arr.ToArray()`)
- ❌ Nested collection expression handling
- ❌ Trivia preservation during transformation

### Reasons for Deferral

#### 1. Rare Use Case (Primary Reason)

**Market Reality:**
- Collection expressions introduced in C# 12 (November 2023)
- Requires .NET 8+ or latest compiler with language version override
- Migration scenario: "I have C# 12 code that must run on older frameworks"

**Usage Statistics:**
- As of January 2025, C# 12 adoption is still ramping up
- Most codebases target C# 7.3 (net48), C# 10 (net6.0), or C# 12 (net8.0+)
- Very few codebases are **downgrading** from C# 12 to C# 11 or lower
- Native support is preferred over syntax conversion

**Typical Migration Paths:**
- ✅ Common: net48 (C# 7.3) → net8.0 (C# 12) - **UPGRADE** (no conversion needed)
- ✅ Common: net6.0 (C# 10) → net8.0 (C# 12) - **UPGRADE** (no conversion needed)
- ❌ Rare: net8.0 (C# 12) → net48 (C# 7.3) - **DOWNGRADE** (collection expression converter needed)
- ❌ Very Rare: Using C# 12 features on net48 with compiler override, then needing to remove them

**Contrast with Tuple Converter:**
- Tuples (C# 7.0, 2017) targeting net35 (C# 3.0, 2008) = **9-year gap**
- Collection expressions (C# 12, 2023) targeting net48 (C# 7.3) = **forward-porting scenario**

**Verdict:** Low demand justifies deferral until real-world usage patterns emerge.

#### 2. Complex Trivia Preservation (Secondary Reason)

Collection expression conversion requires **major structural transformation**:

**Before (C# 12):**
```csharp
var numbers = [1, 2, 3];
var empty = [];
var combined = [..first, ..second];
```

**After (C# 7.3):**
```csharp
var numbers = new[] { 1, 2, 3 };
var empty = Array.Empty<int>();  // Requires type inference!
var combined = first.Concat(second).ToArray();  // or first.Union(second).ToArray()
```

**Technical Challenges:**

1. **Type Inference for Empty Collections**
   - `[]` has no elements → What's the element type?
   - Requires SemanticModel to infer from assignment target
   - Example: `List<string> items = [];` → Must infer `string` from `List<string>`

2. **Spread Element Semantics**
   - `[..arr]` can mean different things based on context
   - Array context: `arr.ToArray()`
   - List context: `new List<T>(arr)`
   - IEnumerable context: `arr` (no conversion)
   - Requires semantic analysis to determine correct conversion

3. **Nested Collection Expressions**
   - `[[1, 2], [3, 4]]` → `new[] { new[] { 1, 2 }, new[] { 3, 4 } }`
   - Recursive visitor pattern with correct precedence

4. **Trivia Preservation**
   - Collection expressions often span multiple lines with custom formatting
   - Converting `[` to `new[] {` while preserving indentation is non-trivial
   - NormalizeWhitespace() conflicts with PreserveTrivia() (same issue as TupleReturnConverter)

**Comparison to Other Converters:**

| Converter | Transformation Complexity | Trivia Complexity | Status |
|-----------|--------------------------|-------------------|--------|
| NullableReferenceTypeStripper | Low (remove `?`) | Low (token removal) | ✅ Implemented (24/24 tests passing) |
| TupleReturnConverter | High (return type + body changes) | **High** (documented limitations) | ⚠️ Placeholder (11/11 tests failing due to trivia) |
| CollectionExpressionConverter | **Very High** (semantic + structural) | **High** (nested structures) | ⏳ Deferred |

**Verdict:** Effort required (~5-7 days) doesn't justify ROI given rare use case.

#### 3. V1 Focus on High-ROI Features (Strategic Reason)

**V1 Priorities** (8-9 weeks):
1. Extract Method (high demand, frequent use)
2. Constructor Injection (high demand, architectural improvement)
3. Make Field Readonly (medium demand, easy wins)
4. Safe Delete (medium demand, safety feature)
5. Extract Class (medium demand, architectural improvement)

**Collection Expression Converter:**
- Demand: Very low (niche migration scenario)
- Frequency: Rare (one-time migration, not daily workflow)
- Value: Limited (workaround: manually rewrite collection expressions)

**Resource Allocation:**
- V1 budget: 40-45 days
- Collection expression full implementation: 5-7 days
- **Opportunity cost:** 5-7 days could be spent on higher-value features

**Verdict:** Deliver converters with clear user demand first.

### Implementation Roadmap

#### When to Implement

**Triggers for Implementation:**

1. **User Demand Threshold**
   - 5+ GitHub issues requesting collection expression downgrading
   - 10+ users asking about C# 12 → C# 11 migration
   - Survey data showing >20% of users need this feature

2. **Ecosystem Maturity**
   - C# 12 adoption reaches 50%+ of .NET projects
   - Common migration path emerges (e.g., Unity games using latest C# but targeting older runtimes)
   - Large codebases using C# 12 features with multi-targeting scenarios

3. **Strategic Value**
   - Integration with broader framework migration tools (V3.0 Migration Assistants)
   - Part of comprehensive "Modernize Code" workflow
   - Completes syntax conversion framework for marketing purposes

**Decision Point:** Q3 2025 (6 months after V1.0 release)
- Review GitHub issue tracker for demand signals
- Survey V1.0 users about syntax conversion needs
- Re-evaluate against V2.5 feature priorities

#### Estimated Effort (When Implemented)

**Implementation:** 5-7 days
- VisitCollectionExpression override: 2 days
- Type inference with SemanticModel: 1-2 days
- Spread element conversion: 1 day
- Trivia preservation: 2 days
- Testing (20+ test cases): 1 day

**Testing:** 2-3 days
- Array collection expressions: 5 tests
- Empty collection inference: 5 tests
- Spread elements: 5 tests
- Nested expressions: 3 tests
- Trivia preservation: 2 tests

**Total:** 7-10 days

**Compare to:**
- NullableReferenceTypeStripper (implemented): 3-4 days → **24/24 tests passing** ✅
- TupleReturnConverter (placeholder): 5-7 days → **11/11 tests failing** (trivia issues) ⚠️
- CollectionExpressionConverter (deferred): 7-10 days → **Architecture proven, implementation when needed** ⏳

#### Implementation Plan (Future V2.5)

**Phase 1: Basic Conversion (3 days)**
```csharp
public override SyntaxNode? VisitCollectionExpression(CollectionExpressionSyntax node)
{
    // Handle simple array initialization: [1, 2, 3] → new[] { 1, 2, 3 }
    if (node.Elements.All(e => e is ExpressionElementSyntax))
    {
        var elements = node.Elements
            .Cast<ExpressionElementSyntax>()
            .Select(e => e.Expression);

        return SyntaxFactory.ImplicitArrayCreationExpression(
            SyntaxFactory.InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SyntaxFactory.SeparatedList(elements)))
            .WithLeadingTrivia(node.GetLeadingTrivia())
            .WithTrailingTrivia(node.GetTrailingTrivia());
    }

    return base.VisitCollectionExpression(node);
}
```

**Phase 2: Type Inference for Empty Collections (2 days)**
```csharp
private TypeSyntax InferElementType(CollectionExpressionSyntax node, SemanticModel semanticModel)
{
    // Get the type of the variable being assigned to
    var parent = node.Parent;
    if (parent is EqualsValueClauseSyntax equalsValue)
    {
        var variableDeclarator = equalsValue.Parent as VariableDeclaratorSyntax;
        var variableDeclaration = variableDeclarator?.Parent as VariableDeclarationSyntax;
        if (variableDeclaration != null)
        {
            var typeInfo = semanticModel.GetTypeInfo(variableDeclaration.Type);
            if (typeInfo.Type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                return SyntaxFactory.ParseTypeName(namedType.TypeArguments[0].ToDisplayString());
            }
        }
    }

    // Fallback: object
    return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword));
}

// Convert [] → Array.Empty<T>()
if (node.Elements.Count == 0)
{
    var elementType = InferElementType(node, semanticModel);
    return SyntaxFactory.InvocationExpression(
        SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.GenericName("Array")
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(elementType))),
            SyntaxFactory.IdentifierName("Empty")));
}
```

**Phase 3: Spread Element Conversion (1 day)**
```csharp
// Handle spread elements: [..arr] → arr.ToArray()
if (node.Elements.All(e => e is SpreadElementSyntax))
{
    // Single spread: [..arr] → arr
    if (node.Elements.Count == 1)
    {
        var spread = (SpreadElementSyntax)node.Elements[0];
        return spread.Expression;
    }

    // Multiple spreads: [..first, ..second] → first.Concat(second).ToArray()
    // ... implementation ...
}
```

**Phase 4: Trivia Preservation (2 days)**
- Study TupleReturnConverter limitations
- Implement sophisticated trivia copying
- Test with real-world formatting scenarios

### Code Locations

**Primary Implementation:**
- `src/RefactorCsharpMCP.Core/SyntaxConversion/CollectionExpressionConverter.cs` (lines 1-84)
  - Class documentation: Lines 7-37
  - Visit method: Lines 55-83

**Test Coverage:**
- `src/RefactorCsharpMCP.Tests/SyntaxConversion/CollectionExpressionConverterTests.cs` (lines 1-98)
  - Class documentation: Lines 8-21
  - Property tests: Lines 23-29
  - Framework compatibility tests: Lines 31-64
  - Placeholder behavior test: Lines 66-89

**Architecture:**
- `src/RefactorCsharpMCP.Core/SyntaxConversion/SyntaxConverterBase.cs` - Base class providing framework checking
- `src/RefactorCsharpMCP.Core/SyntaxConversion/ISyntaxConverter.cs` - Interface defining converter contract
- `src/RefactorCsharpMCP.Core/SyntaxConversion/SyntaxConversionPipeline.cs` (lines 123-135) - Pipeline that will invoke converter when implemented

### Alternative Approaches

#### Option A: Lightweight Validation Only (Current Approach)
- Converter exists for architecture demonstration
- Returns code unchanged (no transformation)
- Clear documentation explains deferral
- **Effort:** 0 days (already complete)
- **Value:** Framework-aware detection works, users warned when targeting incompatible frameworks

#### Option B: Partial Implementation (Simple Cases Only)
- Handle only simple array expressions: `[1, 2, 3]` → `new[] { 1, 2, 3 }`
- Skip empty collections (no type inference)
- Skip spread elements
- **Effort:** 2-3 days
- **Value:** 60% of use cases covered, but incomplete solution may confuse users

#### Option C: Full Implementation (V2.5 or Later)
- Complete conversion including type inference and spread elements
- Sophisticated trivia preservation
- **Effort:** 7-10 days
- **Value:** 100% coverage, production-ready

**Current Decision:** **Option A** until user demand justifies Option C.

### Related Converters for Comparison

#### TupleReturnConverter (Similar Complexity)
- **Status:** Placeholder (like CollectionExpressionConverter)
- **Known Issues:** 11/11 tests failing due to trivia preservation
- **Lesson:** Major structural transformations require significant trivia handling effort
- **Similarity:** Both require semantic analysis (tuple element names vs. collection element types)

#### NullableReferenceTypeStripper (Successful Implementation)
- **Status:** Fully implemented, 24/24 tests passing
- **Reason for Success:** Simple token removal, minimal structural changes
- **Difference:** Collection expressions require complete syntax tree restructuring

### User Guidance

**If you need collection expression downgrading:**

1. **Manual Rewrite (Recommended):**
   ```csharp
   // Change this:
   var numbers = [1, 2, 3];

   // To this:
   var numbers = new[] { 1, 2, 3 };
   ```

2. **Compiler Language Version:**
   ```xml
   <LangVersion>11.0</LangVersion>
   ```
   Set in .csproj to prevent C# 12 features from being used.

3. **Static Analysis:**
   Use IDE/analyzer to detect C# 12 features in older codebases.

4. **Request Implementation:**
   Open GitHub issue with use case details. If demand is sufficient, implementation will be prioritized.

### Decision Authority

**Approved By:** Software Architect (architectural review)
**Date:** 2025-10-13
**Review Cycle:** Quarterly (Q3 2025, Q4 2025, Q1 2026)
**Escalation:** If >5 user requests in single quarter, move to active implementation

---

## V4.0+: Advanced Scenarios

**Timeline:** TBD (12+ months after V1.0)

### 4.1 ASP.NET-Specific Refactorings
- Extract Controller Action
- Convert to Minimal API
- Add Middleware
- Generate API Client

### 4.2 Test Generation
- Generate unit tests for methods
- Generate integration tests for controllers
- Parameterized test generation

### 4.3 Performance Refactorings
- Convert to async/await
- Replace LINQ with loops (performance-critical paths)
- Add caching layers
- Optimize allocations (use ArrayPool, stackalloc)

### 4.4 Security Refactorings
- Add input validation
- Replace hardcoded secrets with configuration
- Add authentication/authorization checks

---

## Linter Support: Detailed Analysis

**Question:** Should RefactorCsharpMCP include linter support?

### ✅ Pros

#### 1. Synergy with Refactoring Workflow
- **Natural pipeline:** Detect issue → Suggest refactoring → Execute fix
- **User expectation:** "Find problems and fix them"
- **AI agent value:** Proactive assistance ("I found 15 style violations, shall I fix them?")

**Example Workflow (AI Agent - Claude Code):**
```
User: "Clean up this code"
Agent: *runs analyze_code*
Agent: "I found:
  - 8 unused usings (IDE0005)
  - 3 fields that could be readonly (IDE0044)
  - 1 long method (87 lines, recommend Extract Method)

  Shall I fix these issues?"
User: "Yes"
Agent: *applies remove_unused_usings, make_field_readonly, extract_method*
Agent: "Done! Reduced file from 487 to 423 lines, improved readability."
```

#### 2. Framework-Aware Analysis (Unique Value)
- **Mike's requirement:** Prevent C# 8+ features in .NET Framework code
- **Competitive advantage:** Most linters aren't framework-aware
- **Integration:** Linter rules use same framework detection as refactorings

**Example:**
```csharp
// Targeting net48 (C# 7.3)
var items = ["a", "b", "c"]; // ❌ Linter error: Collection expressions require C# 12
// Suggested fix: var items = new List<string> { "a", "b", "c" };
```

#### 3. Market Differentiation
- **Most linters:** Report issues only
- **RefactorCsharpMCP:** Analyze + auto-fix via MCP
- **Value proposition:** "AI-powered refactoring with diagnostic guidance"

#### 4. Single Tool for Analyze + Fix
- **User benefit:** No separate linter + manual refactoring
- **Reduced friction:** One MCP server instead of multiple tools
- **Consistency:** Same framework handling for analysis and refactoring

### ❌ Cons

#### 1. Scope Creep & Timeline Risk
- **V1 timeline:** Already 8-9 weeks with 10 refactorings
- **Linter addition:** +3-4 weeks (rules engine, diagnostics, configuration)
- **Risk:** Delays core refactoring capabilities
- **Impact:** Misses V1 delivery window

#### 2. Roslyn Already Provides This
- **Built-in diagnostics:** 500+ CA/IDE rules already exist
- **Redundancy:** Reinventing the wheel
- **Better approach:** Expose existing Roslyn diagnostics (V2.0 plan)

**What Roslyn Provides:**
- Code style (IDE rules)
- Code quality (CA rules)
- Security analysis (CA security rules)
- Performance analysis (CA performance rules)

#### 3. Configuration Complexity
- **User preferences vary:** Braces on new line? var vs explicit types?
- **Configuration files:** .editorconfig, ruleset files, .refactorconfig
- **Product complexity:** Simple refactoring tool → Complex analysis platform
- **Support burden:** "How do I configure rule XYZ?"

#### 4. Maintenance Burden
- **C# evolution:** New language versions require rule updates
- **Rule definitions:** Severity levels, suppressions, categories
- **Ongoing maintenance:** 10 refactorings (one-time) vs 50+ rules (continuous)

#### 5. Different User Mental Model
- **Refactoring:** "I want to improve this code structure" (intentional, targeted)
- **Linting:** "Show me all problems in my codebase" (passive, comprehensive)
- **Product confusion:** What is RefactorCsharpMCP's purpose?

**User Expectations Mismatch:**
- **Linter users expect:** Workspace-wide analysis, hundreds of rules, extensive configuration
- **V1 delivers:** Single-file refactoring, 10 operations, minimal config
- **Result:** Disappointed users ("This linter only checks one file?")

#### 6. Cross-File Analysis Required
- **Good linting needs:** Workspace analysis (unused types, dead code, architectural rules)
- **V1 scope:** Single-file refactoring
- **Capability mismatch:** Linter expectations exceed V1 design

**Examples:**
```csharp
// File: UserRepository.cs
public class UserRepository { } // Is this class used anywhere? Need workspace analysis

// File: Services/IUserService.cs
public interface IUserService { } // Any implementations? Need multi-file search
```

### 🎯 Recommendation: Non-Goal for V1, Phased Approach for V2.0+

#### V1 Strategy: Focus on Core Refactorings
- **Deliver:** 10 high-quality refactorings
- **Nail:** Framework awareness (13 .NET versions)
- **Prove:** MCP integration works for AI agents
- **Timeline:** 8-9 weeks (achievable)

#### V2.0 Strategy: Lightweight Diagnostic Integration
- **Expose:** Existing Roslyn diagnostics (4-5 days effort)
- **Map:** Diagnostics → applicable refactorings
- **Enable:** `analyze_code` → `fix_diagnostic` workflow
- **No custom rules:** Use Roslyn's 500+ built-in rules

**Benefits:**
- Minimal effort (leverage existing infrastructure)
- Valuable integration (linting + fixing)
- No maintenance burden (Roslyn owns rules)

#### V2.5 Strategy: Custom Framework Rules
- **Add:** Framework-specific diagnostics (C# 8+ feature detection)
- **Target:** Mike's critical requirement (prevent modern features in legacy code)
- **Effort:** 6-8 days for 3-5 custom rules

#### V3.0+: Full Linter Features (If Needed)
- Architectural rules (layering violations)
- .editorconfig integration
- Custom rule engine

**Decision criteria:** User demand + competitive analysis after V2.0 release

---

## Alternative: Diagnostic ID Mapping (Zero Effort)

**If you want linter synergy in V1 without any implementation:**

### Add to Existing Refactoring Tool Descriptions

**Example - `remove_unused_usings`:**
```json
{
  "name": "remove_unused_usings",
  "description": "Remove unnecessary using directives from C# code. Fixes Roslyn diagnostics IDE0005 and CS8019.",
  "diagnosticIds": ["IDE0005", "CS8019"]
}
```

**Example - `make_field_readonly`:**
```json
{
  "name": "make_field_readonly",
  "description": "Make fields readonly if only assigned in constructors. Fixes Roslyn diagnostic IDE0044.",
  "diagnosticIds": ["IDE0044"]
}
```

### Benefits
- ✅ **Zero implementation effort:** Just documentation
- ✅ **AI agent awareness:** Claude Code can map linter warnings → refactorings
- ✅ **Future-proof:** Sets up V2.0 `fix_diagnostic` integration
- ✅ **No scope creep:** V1 timeline unchanged

### AI Agent Usage
```
User: "My CI failed with IDE0005 errors"
Agent: *reads refactoring tool descriptions*
Agent: "IDE0005 is 'Unnecessary using directive'. I can fix this with remove_unused_usings tool. Shall I?"
```

**Recommendation:** Add diagnostic ID mappings to V1 tool descriptions (update PRD)

---

## Prioritization Framework

**How to decide what goes into V2.0, V2.5, V3.0?**

### Evaluation Criteria

| Criterion | Weight | Description |
|-----------|--------|-------------|
| **User Value** | 40% | How much does this help Sarah, Mike, or AI agents? |
| **Effort** | 30% | Implementation complexity (days) |
| **Risk** | 20% | Technical risk, architectural impact |
| **Dependencies** | 10% | Does it require other features first? |

### Scoring Examples

#### Cross-File Rename (V2.0)
- User Value: 9/10 (high demand, frequent need)
- Effort: 6/10 (5-7 days, moderate complexity)
- Risk: 4/10 (transactional semantics, rollback needed)
- Dependencies: 2/10 (requires workspace symbol resolution)
- **Score:** 6.8/10 → **High Priority for V2.0**

#### Framework Migration Assistant (V3.0)
- User Value: 8/10 (valuable but infrequent need)
- Effort: 2/10 (15-20 days, high complexity)
- Risk: 3/10 (many edge cases, API compatibility)
- Dependencies: 5/10 (needs cross-file, bulk operations)
- **Score:** 5.2/10 → **V3.0 or later**

#### Diagnostic Integration (V2.0)
- User Value: 8/10 (nice synergy with refactoring)
- Effort: 8/10 (4-5 days, low complexity)
- Risk: 9/10 (low risk, uses existing Roslyn APIs)
- Dependencies: 10/10 (no dependencies)
- **Score:** 8.3/10 → **High Priority for V2.0**

---

## Success Metrics

**How do we measure if V2.0+ features are successful?**

### Quantitative Metrics

#### V2.0: Cross-File Refactoring
- **Adoption rate:** % of users who use cross-file vs single-file refactorings
- **Usage frequency:** Average cross-file refactorings per user per week
- **File count:** Average number of files modified per cross-file refactoring
- **Target:** 60% of users adopt cross-file refactorings within 3 months

#### V2.0: Diagnostic Integration
- **Workflow completion:** % of users who `analyze_code` → `fix_diagnostic`
- **Fix rate:** % of detected diagnostics that get fixed via tool
- **Time saved:** Average time to fix diagnostics (manual vs automated)
- **Target:** 40% of diagnostics auto-fixed via tool

### Qualitative Metrics

#### User Satisfaction
- Survey: "Does diagnostic integration help you find and fix issues faster?"
- NPS score for V2.0 features
- GitHub issues/feature requests related to V2.0 capabilities

#### AI Agent Effectiveness
- Monitor MCP tool invocations (which tools are used most?)
- Success rate (% of refactorings that compile)
- User feedback on AI agent proactive suggestions

---

## Out of Scope (Explicitly Not Planned)

**Features we will NOT build (at least not in foreseeable future):**

### 1. Visual Studio / Rider Extension
- **Why not:** Requires separate UI development
- **Alternative:** MCP integration works with any MCP-compatible client
- **Effort saved:** 6-8 weeks

### 2. Language Support Beyond C#
- **Why not:** Roslyn is C#-specific
- **Alternative:** Other MCP servers for other languages
- **Examples:** F#, VB.NET, TypeScript

### 3. Code Formatting / Prettifying
- **Why not:** dotnet format already exists
- **Alternative:** Use existing formatters
- **Scope:** Refactoring changes structure, not style

### 4. Git Integration
- **Why not:** MCP clients (Claude Code) handle version control
- **Alternative:** Users commit refactored code themselves
- **Scope:** RefactorCsharpMCP transforms code, doesn't manage source control

### 5. Cloud-Based Service
- **Why not:** MCP uses stdio transport (local execution)
- **Alternative:** Users run MCP server locally
- **Reason:** Security (code never leaves user's machine)

---

## Research & Validation

**Before committing to V2.0+ features, conduct:**

### User Research
- **Survey V1 users:** What features do you want most?
- **Usage analytics:** Which V1 refactorings are used most?
- **Pain point interviews:** What refactoring tasks are still manual?

### Competitive Analysis
- **ReSharper refactorings:** What do they offer that we don't?
- **Visual Studio refactorings:** What's built-in?
- **Roslyn analyzers ecosystem:** What gaps exist?

### Technical Feasibility Studies
- **Cross-file performance:** Can we refactor 100-file solutions in <5 seconds?
- **Workspace loading:** How long to load a 50-project solution?
- **Memory constraints:** Can we run in constrained environments (CI/CD)?

---

## Document Maintenance

**This roadmap is a living document.**

### Update Triggers
- After each major release (V1.0, V2.0, etc.)
- Based on user feedback and feature requests
- When competitive landscape changes
- When new Roslyn capabilities become available

### Review Cadence
- **Quarterly:** Review priorities and scoring
- **After V1.0 release:** Finalize V2.0 scope based on user feedback
- **After V2.0 release:** Finalize V2.5 scope

### Stakeholder Input
- **Users:** Feature requests, pain points
- **Contributors:** Implementation complexity assessments
- **AI Agent Teams:** MCP tool API design feedback

---

## Appendix: Estimated Effort Summary

| Version | Features | Effort (Days) | Effort (Weeks) | Timeline |
|---------|----------|---------------|----------------|----------|
| **V1.0** | 10 single-file refactorings + framework awareness | 40-45 | 8-9 | ✅ Current |
| **V2.0** | Cross-file refactoring + diagnostic integration | 27-38 | 5-7 | Q2 2025 |
| **V2.5** | Linter support + custom rules | 18-23 | 3.5-4.5 | Q3 2025 |
| **V3.0** | Smart suggestions + bulk operations | 43-55 | 8-11 | Q4 2025 |
| **V4.0+** | Advanced scenarios (ASP.NET, testing, performance) | TBD | TBD | 2026+ |

**Cumulative Effort:**
- V1.0 → V2.0: 67-83 days (13-17 weeks)
- V1.0 → V2.5: 85-106 days (17-21 weeks)
- V1.0 → V3.0: 128-161 days (25-32 weeks)

---

## Conclusion

This roadmap provides a clear path for RefactorCsharpMCP's evolution beyond V1.0. Key principles:

1. **V1 Focus:** Deliver core refactorings with framework awareness (8-9 weeks)
2. **Phased Approach:** Incremental value delivery (V2.0 every 2-3 months)
3. **User-Driven:** Validate features through research before building
4. **Leverage Roslyn:** Don't reinvent the wheel, extend existing capabilities
5. **MCP Integration:** Optimize for AI agent consumption throughout

**Next Steps:**
1. Deliver V1.0 successfully
2. Gather user feedback and usage data
3. Refine V2.0 scope based on actual user needs
4. Update this roadmap quarterly

**Document Status:** Draft / For Discussion
**Approval Needed:** Product Owner, Software Architect, Stakeholders

---

## Architectural Commentary (Software Architect Review)

**Reviewer:** Master Software Architect
**Date:** 2025-10-10
**Review Focus:** Technical feasibility, architectural soundness, implementation risks, performance implications

---

### V2.0 Cross-File Refactoring: Technical Feasibility Assessment

#### Workspace Architecture Requirements

**CRITICAL FINDING:** Cross-file refactoring requires a **fundamental architectural shift** from single-SyntaxTree operations to workspace-wide MSBuild integration.

**Current V1 Architecture (Single-File):**
```
User Code → Parse to SyntaxTree → SemanticModel → Refactor → Return Modified Code
```
**Time Complexity:** O(1) file, <2 seconds
**Memory:** ~50-100MB per file

**V2.0 Required Architecture (Multi-File):**
```
User Code → Load MSBuildWorkspace → Parse Solution → Create Compilation per Project →
  Find References Across Projects → Batch Updates → Transactional Commit → Return Multiple Files
```
**Time Complexity:** O(N) files, potential 5-30 seconds for large solutions
**Memory:** 500MB-2GB for 50-project solution

**Key Technical Challenges:**

#### 1. MSBuildWorkspace vs. AdhocWorkspace
**Problem:** Loading a full solution via `MSBuildWorkspace.OpenSolutionAsync()` is **EXPENSIVE**:
- .csproj parsing: 50-200ms per project
- NuGet package resolution: 1-5 seconds for complex projects
- Multi-targeting projects (net8.0;net48): 2× compilation time

**Alternatives:**

**Option A: Full MSBuildWorkspace (Accurate but Slow)**
```csharp
var workspace = MSBuildWorkspace.Create();
var solution = await workspace.OpenSolutionAsync(solutionPath);
// 10-30 seconds for 50-project solution
```
**Pros:** Accurate reference resolution, respects .csproj settings
**Cons:** Prohibitively slow for real-time refactoring (30s is unacceptable)

**Option B: AdhocWorkspace (Fast but Limited)**
```csharp
var workspace = new AdhocWorkspace();
foreach (var file in files) {
    workspace.AddDocument(documentInfo); // 1-5ms per file
}
// 1-2 seconds for 1000 files
```
**Pros:** Fast startup (<5 seconds), minimal memory
**Cons:** Missing references, no project-level metadata, cross-project references broken

**Option C: Hybrid Approach (Recommended for V2.0)**
```csharp
// 1. Start with AdhocWorkspace for immediate feedback
var workspace = new AdhocWorkspace();
workspace.AddProjects(relevantProjects); // User-specified scope

// 2. Lazily load full MSBuildWorkspace only for cross-project references
if (symbolUsedInMultipleProjects) {
    var fullWorkspace = await MSBuildWorkspace.OpenProjectAsync(projectPath);
    // Only load affected projects, not entire solution
}
```
**Pros:** Fast common case (<5s), accurate when needed
**Cons:** Complexity in hybrid loading, potential inconsistencies

**Architect Recommendation for V2.0:**
- **Implement Option C (Hybrid)** with user-configurable scope
- **Add "Refactoring Scope" parameter** to MCP tools:
  - `"scope": "current-file"` → V1 behavior (fast)
  - `"scope": "current-project"` → Load single project (~5s)
  - `"scope": "solution"` → Load full solution (~30s, warn user)
  - `"scope": "specified-files"` → AdhocWorkspace with file list (fast, limited)

**Effort Impact:** +5-7 days for hybrid workspace implementation (not included in original estimate)

---

#### 2. Transactional Semantics for Multi-File Updates

**Problem:** Updating 47 files atomically requires **transaction-like behavior**:
- What if file #23 fails to update? Rollback previous 22 files?
- What if user's IDE has file #15 open with unsaved changes?
- What if file system write fails mid-transaction?

**Solution Options:**

**Option A: In-Memory Staging + Batch Commit**
```csharp
var stagedChanges = new Dictionary<string, string>(); // FilePath → NewContent

foreach (var reference in references) {
    var updatedDocument = ApplyRefactoring(reference);
    stagedChanges[updatedDocument.FilePath] = updatedDocument.GetText().ToString();
}

// Validate ALL changes before writing ANY files
if (AllChangesValid(stagedChanges)) {
    // Atomic batch write
    foreach (var (path, content) in stagedChanges) {
        File.WriteAllText(path, content);
    }
} else {
    return RefactoringResult.Failure("Validation failed - no changes applied");
}
```
**Pros:** Rollback is trivial (don't write), validation before commit
**Cons:** Memory overhead (1000 files × 5KB = 5MB, acceptable)

**Option B: File System Transactions (Windows NTFS)**
```csharp
// Use Transactional NTFS (TxF) on Windows
// NOT RECOMMENDED: TxF deprecated since Windows 10
```
**Pros:** OS-level rollback
**Cons:** Windows-only, deprecated, complex

**Option C: Git Integration (Staging Area)**
```csharp
// Create git branch, commit all changes, ask user to review
GitRepository.CreateBranch("refactoring/rename-userdatastore");
// ... apply changes ...
GitRepository.Commit("Renamed UserRepository to UserDataStore");
// Return branch name to user for review
```
**Pros:** User can review before merge, natural rollback (delete branch)
**Cons:** Requires git, complex integration, out of V1 scope

**Architect Recommendation for V2.0:**
- **Implement Option A (In-Memory Staging)** - simple, cross-platform, sufficient
- **Add "dry-run" mode** to all cross-file refactorings:
  - Returns list of files that WOULD be modified
  - Returns diff preview for each file
  - User confirms before actual writes

**Effort Impact:** +3-4 days for staging infrastructure and validation (not included in original estimate)

---

#### 3. Performance: Reference Finding Across 1000+ Files

**Roslyn's `SymbolFinder.FindReferencesAsync()` Performance:**
- **Small project (10 files, 5K LOC):** 100-500ms ✅ Acceptable
- **Medium project (100 files, 50K LOC):** 2-5 seconds ⚠️ Borderline
- **Large project (1000 files, 500K LOC):** 30-60 seconds ❌ **UNACCEPTABLE**

**Why So Slow?**
1. Must load and parse all syntax trees
2. Must create semantic models for all files
3. Must traverse syntax nodes in every file

**Optimization Strategies:**

**Strategy 1: Parallel Processing**
```csharp
var references = await SymbolFinder.FindReferencesAsync(symbol, solution);

// Process files in parallel (8-core CPU = 8× speedup)
var updates = await Task.WhenAll(
    references.Select(async refLocation =>
        await ProcessReferenceAsync(refLocation)
    )
);
```
**Expected Improvement:** 4-8× faster on multi-core CPUs
**Risk:** Thread-safe document updates required

**Strategy 2: Incremental Loading**
```csharp
// Don't load entire solution - only load projects that reference the symbol
var referencingProjects = await FindReferencingProjectsAsync(symbol);
// If symbol is private → only 1 project
// If symbol is internal → only projects with InternalsVisibleTo
// If symbol is public → potentially all projects (worst case)

foreach (var project in referencingProjects) {
    var compilation = await project.GetCompilationAsync();
    // Process this project only
}
```
**Expected Improvement:** 10-100× faster for private/internal symbols
**Risk:** Must correctly detect symbol visibility

**Strategy 3: Index-Based Search (Future V2.5)**
```csharp
// Pre-build symbol index for large solutions
// Similar to Visual Studio's IntelliSense database
var index = await SymbolIndexBuilder.BuildIndexAsync(solution);
var references = index.FindReferences(symbolName, symbolKind);
// <100ms even for 100K-file solutions
```
**Expected Improvement:** 100-1000× faster
**Effort:** 10-15 days for index implementation

**Architect Recommendation for V2.0:**
- **Implement Strategy 1 + Strategy 2** (parallel + incremental) - achievable in V2.0 timeline
- **Document performance expectations:**
  - Private/internal symbols: <5 seconds
  - Public symbols in <50 projects: <10 seconds
  - Public symbols in >50 projects: 30-60 seconds (warn user)
- **Defer Strategy 3 (indexing)** to V2.5

**Effort Impact:** +4-5 days for parallel processing and incremental loading (CRITICAL for usability)

---

### V2.0 Diagnostic Integration: Roslyn APIs Analysis

#### Exposing Roslyn Diagnostics: Simpler Than It Looks

**Good News:** `analyze_code` tool is **TRIVIALLY EASY** to implement. Roslyn does all the work:

```csharp
public async Task<DiagnosticResult> AnalyzeCode(string sourceCode, string targetFramework)
{
    // 1. Parse code
    var tree = CSharpSyntaxTree.ParseText(sourceCode);

    // 2. Create compilation with target framework
    var compilation = CreateCompilation(tree, targetFramework);

    // 3. Get diagnostics (Roslyn provides 500+ built-in analyzers)
    var diagnostics = compilation.GetDiagnostics()
        .Where(d => d.Severity >= DiagnosticSeverity.Warning)
        .ToList();

    // 4. Map to applicable refactorings
    return new DiagnosticResult {
        Diagnostics = diagnostics.Select(d => new DiagnosticInfo {
            Id = d.Id,
            Message = d.GetMessage(),
            Location = d.Location.GetLineSpan(),
            ApplicableRefactorings = MapDiagnosticToRefactorings(d.Id)
        })
    };
}
```

**Implementation Effort:** **2-3 days** (not 4-5 days as estimated)

**Why So Easy?**
- Roslyn compilation automatically runs all CA/IDE analyzers
- No custom rule engine needed
- No configuration needed (respects .editorconfig if present)
- Mapping diagnostic IDs → refactorings is a simple dictionary lookup

**Architect Recommendation:**
- **Move diagnostic integration to V1.5** (between V1 and V2.0) as a "quick win"
- **Effort:** 2-3 days
- **Value:** Immediate synergy with linting workflows
- **Risk:** Near zero (leverages existing Roslyn infrastructure)

---

#### `fix_diagnostic` Tool: Implementation Strategy

**Approach:** **Dispatcher Pattern** - Route diagnostic ID to appropriate refactoring:

```csharp
public RefactoringResult FixDiagnostic(
    string sourceCode,
    string diagnosticId,
    int line,
    int column,
    string targetFramework)
{
    return diagnosticId switch {
        "IDE0005" or "CS8019" =>
            new RemoveUnusedUsings().Execute(sourceCode, targetFramework),

        "IDE0044" =>
            new MakeFieldReadonly().Execute(sourceCode, fieldName, targetFramework),

        "IDE0058" or "IDE0059" =>
            new InlineVariable().Execute(sourceCode, line, variableName, targetFramework),

        "IDE0022" =>
            new InlineMethod().Execute(sourceCode, className, methodName, targetFramework),

        _ => RefactoringResult.Failure(
            $"No refactoring available for diagnostic {diagnosticId}")
    };
}
```

**Implementation Effort:** **1-2 days** (not 3-4 days as estimated)

**Why So Easy?**
- Reuses existing V1 refactorings
- Just needs routing logic and parameter extraction from diagnostic location

**Architect Recommendation:**
- **Implement in V1.5** alongside `analyze_code`
- **Total V1.5 effort:** 3-5 days (diagnostic integration complete)

---

### V2.5 Custom Rules: Implementation Strategy

#### Framework-Specific Diagnostics: Roslyn Analyzer Development

**WARNING:** Custom diagnostic rules require **Roslyn Analyzer** development, which is SUBSTANTIALLY more complex than estimated.

**Reality Check for RCMCP0001 (Unsupported Language Feature):**

**Estimated:** 6-8 days for 3 rules
**Realistic:** 12-18 days for 3 rules

**Why?**

1. **Custom Roslyn Analyzer Development** (not simple rule definitions):
```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnsupportedLanguageFeatureAnalyzer : DiagnosticAnalyzer
{
    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AnalyzeCollectionExpression,
            SyntaxKind.CollectionExpression);
        context.RegisterSyntaxNodeAction(AnalyzeRecordDeclaration,
            SyntaxKind.RecordDeclaration);
        // ... 20+ more syntax kinds to detect
    }

    private void AnalyzeCollectionExpression(SyntaxNodeAnalysisContext context)
    {
        // Get target framework from compilation options
        var targetFramework = GetTargetFramework(context.Compilation);
        var languageVersion = MapFrameworkToLanguageVersion(targetFramework);

        if (languageVersion < LanguageVersion.CSharp12) {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule_RCMCP0001,
                context.Node.GetLocation(),
                "Collection expressions", "C# 12", targetFramework));
        }
    }
}
```

2. **Testing Each Rule Across 13 Frameworks:**
   - 3 rules × 13 frameworks × 5 test cases each = 195 tests
   - Each test must compile and run analyzer
   - Roslyn analyzer testing framework is verbose

3. **Packaging and Distribution:**
   - NuGet package for analyzer
   - Visual Studio extension for IDE integration
   - MSBuild integration for CI/CD
   - **OR:** Integrate into MCP server (simpler but non-standard)

**Architect Recommendation:**
- **Revise V2.5 estimate:** 12-18 days (not 6-8 days)
- **Alternative Lightweight Approach:** Don't write full Roslyn analyzers, just add validation to refactoring tools:
  ```csharp
  // In each refactoring tool, BEFORE generating code:
  private void ValidateOutputSyntax(SyntaxNode output, LanguageVersion target)
  {
      if (output.DescendantNodes().OfType<CollectionExpressionSyntax>().Any()
          && target < LanguageVersion.CSharp12)
      {
          throw new UnsupportedLanguageFeatureException(
              "Collection expressions require C# 12", "RCMCP0001");
      }
  }
  ```
  **Effort:** 3-4 days (validation in refactorings, not separate analyzers)
  **Trade-off:** Only detects issues during refactoring, not as IDE live analysis

---

### V3.0 Smart Suggestions: AI Integration Architecture

#### Code Smell Detection: Metrics-Based vs. AI-Based

**Two Approaches:**

**Approach A: Rule-Based Metrics (Traditional, Deterministic)**
```csharp
public List<RefactoringSuggestion> SuggestRefactorings(SyntaxTree tree)
{
    var suggestions = new List<RefactoringSuggestion>();

    // Long method detection
    foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
    {
        var lineCount = method.Body.GetLocation().GetLineSpan().EndLinePosition.Line
                      - method.Body.GetLocation().GetLineSpan().StartLinePosition.Line;

        if (lineCount > 50) {
            suggestions.Add(new RefactoringSuggestion {
                Type = "extract_method",
                Reason = $"Method has {lineCount} lines (threshold: 50)",
                Priority = lineCount > 100 ? "high" : "medium"
            });
        }
    }

    // God class detection
    var types = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
    foreach (var type in types) {
        var methodCount = type.Members.OfType<MethodDeclarationSyntax>().Count();
        if (methodCount > 15) {
            suggestions.Add(new RefactoringSuggestion {
                Type = "extract_class",
                Reason = $"Class has {methodCount} methods (threshold: 15)",
                Priority = "medium"
            });
        }
    }

    return suggestions;
}
```
**Pros:** Fast (<100ms), deterministic, no dependencies
**Cons:** High false positive rate, doesn't understand context
**Effort:** 6-8 days

**Approach B: LLM-Based Analysis (Modern, Context-Aware)**
```csharp
public async Task<List<RefactoringSuggestion>> SuggestRefactoringsAI(
    SyntaxTree tree,
    ILanguageModel llm)
{
    var prompt = $@"
        Analyze this C# code for refactoring opportunities.
        Focus on: long methods, god classes, duplicate code, feature envy.

        Code:
        {tree.GetRoot().ToFullString()}

        Return JSON array of suggestions with: type, reason, priority, location.
    ";

    var response = await llm.CompleteAsync(prompt);
    return JsonSerializer.Deserialize<List<RefactoringSuggestion>>(response);
}
```
**Pros:** Context-aware, low false positives, understands intent
**Cons:** Slow (2-10 seconds), requires LLM API, non-deterministic
**Effort:** 12-15 days (includes LLM integration, prompt engineering, result parsing)

**Architect Recommendation:**
- **V3.0: Implement Approach A (Rule-Based)** first
- **V3.5: Add Approach B (LLM-Based)** as enhancement
- **Hybrid Strategy:** Use rule-based for fast filtering, LLM for validation:
  ```csharp
  var candidates = DetectCodeSmellsWithRules(tree); // <100ms
  var validated = await ValidateWithLLM(candidates); // 2-5 seconds
  return validated; // High confidence suggestions only
  ```

---

### Performance & Scalability Concerns

#### V2.0 Cross-File Operations: Memory and Time Complexity

**Workspace Loading Memory Profile:**

| Solution Size | Projects | Files | LOC | Memory (MSBuildWorkspace) | Memory (AdhocWorkspace) |
|--------------|----------|-------|-----|---------------------------|-------------------------|
| Small | 1-5 | 10-50 | 1K-10K | 100-300MB | 50-100MB |
| Medium | 5-20 | 50-500 | 10K-100K | 300MB-1GB | 100-300MB |
| Large | 20-100 | 500-5K | 100K-1M | 1-3GB | 300MB-1GB |
| **Enterprise** | **100-500** | **5K-50K** | **1M-10M** | **3-10GB** | **1-5GB** |

**Critical Observation:** Enterprise-scale solutions (100+ projects) will **EXCEED TYPICAL SERVER MEMORY** (8-16GB) if not careful.

**Memory Optimization Strategies:**

**Strategy 1: Lazy Project Loading**
```csharp
// Don't load all projects upfront
var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: ct);

// Only load projects that contain the symbol
var relevantProjects = await FindProjectsContainingSymbolAsync(symbol, solution);

foreach (var project in relevantProjects) {
    var compilation = await project.GetCompilationAsync(ct);
    // Process and immediately dispose
    compilation = null;
    GC.Collect(); // Force cleanup
}
```

**Strategy 2: Streaming File Processing**
```csharp
// Don't load all files into memory simultaneously
await foreach (var file in GetFilesContainingReferencesAsync(symbol))
{
    var document = await workspace.GetDocumentAsync(file.Id);
    var updatedDocument = ApplyRefactoring(document);
    await SaveDocumentAsync(updatedDocument);

    // Release memory immediately
    document = null;
    updatedDocument = null;
}
```

**Strategy 3: Memory-Mapped Files for Large Solutions**
```csharp
// For enterprise solutions (>10GB), use disk-backed storage
var tempFile = Path.GetTempFileName();
using var mmf = MemoryMappedFile.CreateFromFile(tempFile, FileMode.Create, null, solutionSize);
// Process in chunks
```

**Architect Recommendation for V2.0:**
- **Implement Strategy 1 + Strategy 2** - sufficient for solutions up to 100 projects
- **Document system requirements:**
  - Small/Medium solutions: 4GB RAM minimum, 8GB recommended
  - Large solutions: 16GB RAM minimum
  - Enterprise solutions: 32GB RAM recommended OR use external workspace service
- **Add memory monitoring** to MCP tools:
  ```json
  {
    "warning": "Solution size 83 projects (420MB memory). Consider scoping refactoring to specific projects."
  }
  ```

---

#### Timeout Strategy for Long-Running Operations

**V1 Target:** <2 seconds (single-file)
**V2 Reality:** 5-60 seconds (cross-file, depending on scope)

**User Experience Problem:** 60-second blocking operation is **UNACCEPTABLE** for AI agent workflows.

**Solution: Async Operation Pattern**

**Option A: Background Job + Polling**
```json
// User calls cross-file rename
POST /refactor/rename
{
  "symbol": "UserRepository",
  "newName": "UserDataStore",
  "scope": "solution"
}

// Immediate response with job ID
{
  "jobId": "abc-123",
  "status": "running",
  "estimatedDuration": "30-60 seconds",
  "progressUrl": "/jobs/abc-123/progress"
}

// Client polls for completion
GET /jobs/abc-123
{
  "status": "complete",
  "filesModified": 47,
  "result": { ... }
}
```

**Option B: Streaming Progress Updates**
```json
// Server streams progress events
{
  "event": "progress",
  "message": "Loading workspace...",
  "percentComplete": 10
}
{
  "event": "progress",
  "message": "Finding references (12/47 files scanned)",
  "percentComplete": 45
}
{
  "event": "complete",
  "result": { ... }
}
```

**Architect Recommendation:**
- **V2.0: Implement Option A (Background Jobs)** - simpler, works with MCP stdio
- **V2.5: Consider Option B (Streaming)** if users demand real-time feedback
- **Critical:** All cross-file operations must support cancellation:
  ```csharp
  public async Task<RefactoringResult> ExecuteCrossFile(
      ...,
      CancellationToken cancellationToken)
  {
      foreach (var file in files) {
          cancellationToken.ThrowIfCancellationRequested();
          // Process file
      }
  }
  ```

---

### Alternative Architectural Approaches

#### Alternative 1: Refactoring as a Service (Cloud-Based)

**Current Plan:** Local MCP server (stdio transport)

**Alternative:** Cloud-based refactoring service (HTTP API)

**Architecture:**
```
Claude Code → HTTP POST to RefactorCsharpMCP.cloud →
  Cloud workers process refactoring →
  Return results via webhook/polling
```

**Pros:**
- ✅ Unlimited memory/CPU (scale horizontally)
- ✅ Pre-loaded workspace caches (faster warm starts)
- ✅ Centralized monitoring and telemetry
- ✅ No local installation required

**Cons:**
- ❌ **Privacy concerns:** User code leaves machine (DEALBREAKER for enterprises)
- ❌ **Latency:** Network roundtrip adds 200-1000ms
- ❌ **Cost:** Cloud infrastructure costs ($500-2000/month for production)
- ❌ **MCP protocol violation:** MCP is designed for local stdio

**Architect Verdict:** **DO NOT PURSUE** cloud-based approach for V1-V3.
**Rationale:** Privacy and security requirements outweigh performance benefits.

---

#### Alternative 2: Language Server Protocol (LSP) Integration

**Current Plan:** MCP-based integration with AI agents

**Alternative:** LSP server for IDE integration (Visual Studio Code, Visual Studio, Rider)

**Architecture:**
```
IDE (VS Code) → LSP Client →
  RefactorCsharpMCP LSP Server →
  Roslyn refactorings →
  Return code actions to IDE
```

**Pros:**
- ✅ Native IDE integration (no AI agent required)
- ✅ Real-time refactoring suggestions (as user types)
- ✅ Familiar developer UX (same as ReSharper, Visual Studio)

**Cons:**
- ❌ **Different target audience:** IDE users, not AI agents
- ❌ **Effort:** 8-12 weeks for full LSP implementation
- ❌ **Maintenance:** Must track LSP spec changes
- ❌ **Competition:** ReSharper, Visual Studio already provide comprehensive refactorings

**Architect Verdict:** **DEFER to V4.0+ or separate product**
**Rationale:** MCP integration is unique value proposition. LSP integration is "me-too" feature.

---

#### Alternative 3: Roslyn Workspaces as Persistent Service

**Current Plan:** Load workspace on-demand for each refactoring

**Alternative:** Keep Roslyn workspace loaded in background service, serve refactorings from warm cache

**Architecture:**
```
RefactorCsharpMCP Daemon (long-running process) →
  Watches .csproj files for changes →
  Maintains hot Roslyn workspace →
  Serves refactoring requests in <1 second
```

**Pros:**
- ✅ **Dramatically faster:** Warm workspace eliminates 5-30 second startup
- ✅ **Better memory management:** Single workspace shared across refactorings
- ✅ **Incremental updates:** Only reparse changed files

**Cons:**
- ❌ **Deployment complexity:** Requires daemon management (start/stop/restart)
- ❌ **Memory footprint:** 1-3GB continuously running
- ❌ **Stale workspace risk:** User changes file outside of daemon's watch

**Architect Verdict:** **STRONG CANDIDATE for V2.5**
**Rationale:** Solves performance problem without cloud infrastructure. Worth the daemon complexity.

**Recommended Implementation (V2.5):**
```csharp
// RefactorCsharpMCP.Daemon
public class WorkspaceService : IHostedService
{
    private MSBuildWorkspace _workspace;
    private Solution _solution;

    public async Task StartAsync(CancellationToken ct)
    {
        _workspace = MSBuildWorkspace.Create();
        _solution = await _workspace.OpenSolutionAsync(solutionPath, ct);

        // Watch file system for changes
        var watcher = new FileSystemWatcher(solutionDirectory, "*.cs");
        watcher.Changed += async (s, e) => await ReloadDocumentAsync(e.FullPath);
    }

    public async Task<RefactoringResult> ExecuteRefactoring(...)
    {
        // Use pre-loaded _solution - instant access
        var document = _solution.GetDocument(documentId);
        // ... apply refactoring ...
        return result;
    }
}
```
**Effort:** 8-10 days for daemon infrastructure + file watching

---

### Architect's Recommendations (Prioritized)

#### Critical for V2.0 Success
1. ✅ **Hybrid Workspace Loading** (AdhocWorkspace + lazy MSBuildWorkspace) - **+5-7 days**
2. ✅ **Transactional Multi-File Updates** (in-memory staging + dry-run) - **+3-4 days**
3. ✅ **Parallel Reference Finding** (4-8× speedup on multi-core CPUs) - **+4-5 days**
4. ✅ **Memory Monitoring & Warnings** (prevent OOM for large solutions) - **+2 days**

**Total Added Effort for V2.0:** **+14-18 days** (not included in original 27-38 day estimate)
**Revised V2.0 Effort:** **41-56 days (8-11 weeks)**

#### Quick Wins for V1.5 (Between V1 and V2)
1. ✅ **Diagnostic Integration** (`analyze_code` + `fix_diagnostic`) - **3-5 days**
2. ✅ **Diagnostic ID Mappings** (add to V1 tool descriptions) - **0 days (documentation only)**

#### Deferred to V2.5 or Later
1. ⏳ **Workspace Daemon** (persistent Roslyn workspace) - **8-10 days** (HIGH VALUE)
2. ⏳ **Custom Roslyn Analyzers** (framework-specific rules) - **12-18 days** (or use lightweight validation)
3. ⏳ **Symbol Index** (for enterprise-scale solutions) - **10-15 days**

#### Explicitly NOT Recommended
1. ❌ **Cloud-Based Service** (privacy concerns, violates MCP model)
2. ❌ **LSP Integration** (different target audience, high effort, crowded market)

---

### Additional Ideas from Architectural Perspective

#### Idea 1: Refactoring Conflict Detection

**Problem Not Addressed in Roadmap:** What if user applies Extract Method while another developer renames a method in same file?

**Solution: Git Integration with Merge Conflict Detection**
```csharp
public RefactoringResult ExecuteWithConflictDetection(...)
{
    // 1. Check if file has been modified since user loaded it
    var fileHash = ComputeFileHash(filePath);
    var gitHash = GetGitHeadHash(filePath);

    if (fileHash != gitHash) {
        return RefactoringResult.Failure(
            "File has been modified since loading. Refresh and retry.",
            ErrorCode.FILE_MODIFIED);
    }

    // 2. Apply refactoring
    var result = ApplyRefactoring(...);

    // 3. Stage as git commit (optional)
    GitRepository.StageChanges(filePath);

    return result;
}
```
**Effort:** 3-4 days
**Value:** Prevents overwriting concurrent changes
**Recommended for:** V2.5 (after cross-file refactoring is stable)

---

#### Idea 2: Refactoring Undo/Redo Stack

**Problem Not Addressed in Roadmap:** User applies 5 refactorings, realizes #3 was wrong, wants to undo just #3

**Solution: Refactoring Transaction Log**
```csharp
public class RefactoringHistory
{
    private Stack<RefactoringTransaction> _history = new();

    public RefactoringResult ApplyAndRecord(IRefactoring refactoring, ...)
    {
        var before = ReadFile(filePath);
        var result = refactoring.Execute(...);
        var after = result.RefactoredCode;

        _history.Push(new RefactoringTransaction {
            Type = refactoring.GetType().Name,
            Before = before,
            After = after,
            FilePath = filePath,
            Timestamp = DateTime.UtcNow
        });

        return result;
    }

    public void Undo(int transactionId)
    {
        var transaction = _history.FirstOrDefault(t => t.Id == transactionId);
        File.WriteAllText(transaction.FilePath, transaction.Before);
    }
}
```
**Effort:** 4-5 days
**Value:** Safety net for experimentation
**Recommended for:** V3.0 (nice-to-have, not critical)

---

#### Idea 3: Refactoring Preview with Side-by-Side Diff

**Problem Not Addressed in Roadmap:** User can't see what changed before applying refactoring

**Solution: Diff Generation Before Commit**
```csharp
public RefactoringPreview GeneratePreview(IRefactoring refactoring, ...)
{
    var before = ReadFile(filePath);
    var result = refactoring.Execute(...);
    var after = result.RefactoredCode;

    // Generate unified diff (like git diff)
    var diff = DiffGenerator.CreateUnifiedDiff(before, after);

    return new RefactoringPreview {
        Diff = diff,
        FilePath = filePath,
        Summary = result.Message,
        Apply = () => File.WriteAllText(filePath, after),
        Reject = () => { /* do nothing */ }
    };
}
```
**Effort:** 2-3 days
**Value:** Builds user confidence
**Recommended for:** V2.0 (should be included in cross-file refactoring)

---

#### Idea 4: AI-Powered Refactoring Parameter Inference

**Problem Not Addressed in Roadmap:** User says "extract this method" but doesn't specify name, parameters, etc.

**Solution: LLM-Based Parameter Suggestion**
```csharp
public async Task<RefactoringParameters> InferParameters(
    string userIntent,
    string sourceCode,
    ILanguageModel llm)
{
    var prompt = $@"
        User wants to: {userIntent}
        Source code: {sourceCode}

        Infer refactoring parameters:
        - Method name (PascalCase, descriptive)
        - Should it be static or instance?
        - Should it be public or private?

        Return JSON.
    ";

    var response = await llm.CompleteAsync(prompt);
    return JsonSerializer.Deserialize<RefactoringParameters>(response);
}
```
**Effort:** 6-8 days (LLM integration + prompt engineering)
**Value:** Reduces friction for AI agent workflows
**Recommended for:** V3.0 (after smart suggestions are implemented)

---

### Final Architectural Assessment

**Overall Roadmap Quality:** **Excellent** - Well-structured, realistic timelines, clear prioritization

**Key Strengths:**
- ✅ Phased approach (incremental value delivery)
- ✅ Leverages existing Roslyn infrastructure (don't reinvent wheel)
- ✅ Clear user personas (Sarah, Mike, AI agents)
- ✅ Explicit non-goals (prevents scope creep)

**Key Concerns:**
- ⚠️ **V2.0 effort underestimated:** +14-18 days for critical infrastructure
- ⚠️ **V2.5 custom analyzers underestimated:** +6-10 days (or use simpler validation approach)
- ⚠️ **Performance implications not fully addressed:** Large solutions (100+ projects) may hit memory limits

**Revised Timeline:**
| Version | Original Estimate | Architect Revised | Confidence |
|---------|------------------|-------------------|------------|
| V1.0 | 8-9 weeks | 8-9 weeks | ✅ High (95%) |
| V2.0 | 5-7 weeks | 8-11 weeks | ⚠️ Medium (75%) - depends on workspace architecture |
| V2.5 | 3.5-4.5 weeks | 5-7 weeks | ⚠️ Medium (70%) - custom analyzers complex |
| V3.0 | 8-11 weeks | 10-14 weeks | ⚠️ Low (60%) - AI integration risks |

**Critical Success Factors for V2.0:**
1. **Workspace loading performance** must be <10 seconds for typical solutions
2. **Memory consumption** must stay under 2GB for 100-project solutions
3. **User communication** about long-running operations (progress bars, cancellation)
4. **Transactional updates** must be rock-solid (no corrupted files)

**Recommendation:** **APPROVE roadmap with timeline adjustments** noted above. Focus on V1.0 delivery first, then validate V2.0 architecture with prototype before committing to timeline.

---

**Reviewed by:** Master Software Architect
**Date:** 2025-10-10
**Confidence Level:** High (90%) for V1-V2, Medium (70%) for V3+
**Next Review:** Post-V1.0 release, before finalizing V2.0 scope
