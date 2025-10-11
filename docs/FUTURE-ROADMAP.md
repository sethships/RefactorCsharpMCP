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

**V2.5 Total Effort:** 18-23 days (3.5-4.5 weeks)

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
