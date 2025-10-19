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

### Target Users

**Primary User: AI Coding Agents (via MCP Protocol)**
- Claude Code, Cursor, Continue, and other AI development tools
- Agents request refactoring operations on behalf of human developers
- **Important:** While agents are the direct consumers of this tool, they work hand-in-hand with human developers who know their codebase and can help direct the agents
- Agents have access to project context (e.g., .csproj files) to determine target framework
- Human developers can guide agents to specify the correct framework version
- Need reliable, framework-compatible code generation

**Secondary User: CLI/Direct Integration Users**
- Developers using RefactorCsharpMCP directly via command line or custom integrations
- Have full context of their project's framework version
- Need predictable, reproducible refactoring results
- May integrate RefactorCsharpMCP into custom build/refactoring pipelines

**User Workflow Assumptions:**
1. Agent or user has access to project file (.csproj) or build configuration
2. Human developer can provide framework version when agent asks
3. Agent or user is responsible for specifying correct framework version
4. Human developer reviews and approves refactoring results (agent-assisted workflow)

### Future Enhancements (Post-v1.0.0)
- 🔮 **Automatic framework detection from project files** - Parse .csproj to auto-populate targetFramework parameter (would reduce caller burden but adds complexity)
- 🔮 **End-of-life framework support** - Support EOL versions with warnings (requires considered support strategy and resource commitment)
- 🔮 **Request throttling** - Rate limiting per client/agent to prevent abuse and ensure fair resource usage
- 🔮 **Response caching** - Cache refactoring results for identical inputs to improve performance and reduce compute costs
- 🔮 **Alpine Linux production images** - Switch to minimal Alpine-based containers for 40% performance improvement and 50% smaller image size (~100MB vs ~200MB)
- 🔮 Framework version upgrading (e.g., .NET Framework 4.6.2 → .NET 8)
- 🔮 Code modernization suggestions
- 🔮 Migration tooling or recommendations
- 🔮 Breaking change detection between frameworks

### Non-Goals (Explicitly Out of Scope for v1.0.0)
- ❌ Smart defaults or fallback framework versions (eliminates ambiguity)
- ❌ Framework detection via heuristics or code analysis (unreliable)

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

### 2.4 End-of-Life Versions (NOT Supported - Future Enhancement)

The following versions are **explicitly NOT supported** in the initial implementation:

| Version | EOL Date | Reason |
|---------|----------|--------|
| .NET 7 | May 14, 2024 | Out of support - security risk |
| .NET 6 | Nov 12, 2024 | Out of support - security risk |
| .NET 5 | May 10, 2022 | Out of support - security risk |
| .NET Core 3.1 | Dec 13, 2022 | Out of support - security risk |
| .NET Core 3.0 | Mar 3, 2020 | Out of support - security risk |
| .NET Core 2.2 | Dec 23, 2019 | Out of support - security risk |
| .NET Core 2.1 | Aug 21, 2021 | Out of support - security risk |
| .NET Core 2.0 | Oct 1, 2018 | Out of support - security risk |
| .NET Framework 4.6.1 | Apr 26, 2022 | Out of support - security risk |
| .NET Framework 4.6 | Apr 26, 2022 | Out of support - security risk |
| .NET Framework 4.5.2 | Apr 26, 2022 | Out of support - security risk |

**Implementation:** Tool will **reject EOL frameworks** with clear error messages.

**Rationale:**
- Supporting EOL frameworks requires significant ongoing maintenance without a considered support strategy
- EOL frameworks pose security risks that we should not soft-endorse by providing tooling support
- Users can work around this limitation (see workaround below)
- EOL support may be added as a **future enhancement** with proper planning and resource allocation

**Workaround for Legacy Projects:**
Users needing to refactor legacy code on EOL frameworks can specify the **nearest supported framework version** as a parameter. For example:
- `.NET Framework 4.5.2` project → specify `"net462"` (C# 7.3)
- `.NET 6` project → specify `"net8.0"` (C# 12)

The tool will generate code compatible with the specified framework's C# version. Users should review generated code to ensure compatibility with their actual runtime.

### 2.5 DevTools Repository Impact

**Current DevTools Projects vs Support Status:**

| Project | Current Framework | Support Status | Refactoring Approach |
|---------|------------------|----------------|---------------------|
| BackupTool | .NET Framework 4.5.2 | ❌ EOL (Apr 2022) | **Workaround:** Specify `"net462"` to refactor |
| LineCounter | .NET Framework 4.5.2 | ❌ EOL (Apr 2022) | **Workaround:** Specify `"net462"` to refactor |
| Logging | .NET Framework 4.5.2 | ❌ EOL (Apr 2022) | **Workaround:** Specify `"net462"` to refactor |
| passgen | .NET 8 | ✅ Supported | Fully supported - specify `"net8.0"` |
| RefactorCsharpMCP | .NET 8 | ✅ Supported | Fully supported - specify `"net8.0"` |

**Important:** Legacy DevTools projects on .NET Framework 4.5.2 cannot directly specify their actual framework version. Users must specify `"net462"` (the nearest supported version) and manually verify generated code is compatible with 4.5.2 runtime. This encourages migration to supported frameworks while providing a practical workaround.

## 3. Technical Design

> **📄 Implementation Details:** Complete C# code, class definitions, and API signatures are documented in [SDD-Framework-Version-Awareness.md](SDD-Framework-Version-Awareness.md)

### 3.1 Framework to C# Language Version Mapping

**Conceptual Mapping:**

The system maintains a mapping from Target Framework Moniker (TFM) to C# Language Version:

**Supported Frameworks:**
- Modern .NET (net9.0, net8.0) → C# 13, C# 12
- .NET Framework (net481 through net462) → C# 7.3
- .NET Framework 3.5 SP1 (net35) → C# 3.0
- .NET Standard (netstandard2.1, netstandard2.0) → C# 8, C# 7.3

**EOL Frameworks (NOT Supported):**
- .NET Framework EOL (net461, net46, net452, net451, net45) → **Rejected** with error
- Modern .NET EOL (net7.0, net6.0, net5.0) → **Rejected** with error
- .NET Core EOL (netcoreapp3.1, netcoreapp3.0, netcoreapp2.x) → **Rejected** with error

Users must specify the nearest supported framework version as a workaround.

See [SDD Section 3](SDD-Framework-Version-Awareness.md#3-framework-mapping-implementation) for complete mapping tables.

### 3.2 Error Handling and Message Taxonomy

**Validation Result Structure:**

The framework validator returns a structured result containing:
- **IsValid**: Whether the framework moniker is properly formatted
- **IsSupported**: Whether Microsoft currently supports this framework
- **IsEOL**: Whether this is an end-of-life framework
- **ErrorMessage**: Human-readable error description
- **SuggestedFramework**: Nearest supported framework (for EOL cases)

**Error Code Taxonomy:**

All errors include a standardized `errorCode` field for programmatic handling:

| Error Code | Category | HTTP Analogy | Description |
|------------|----------|--------------|-------------|
| `EOL_FRAMEWORK` | ValidationError | 400 Bad Request | End-of-life framework specified |
| `INVALID_TFM_FORMAT` | ValidationError | 400 Bad Request | Malformed TFM string |
| `MISSING_PARAMETER` | ValidationError | 400 Bad Request | Required parameter not provided |
| `UNKNOWN_FRAMEWORK` | ValidationError | 400 Bad Request | Valid format but unrecognized version |
| `REFACTORING_FAILED` | ExecutionError | 422 Unprocessable | Framework valid but refactoring failed |
| `SYNTAX_ERROR` | ExecutionError | 422 Unprocessable | Source code has syntax errors |
| `NO_METHOD_FOUND` | ExecutionError | 404 Not Found | Target method/code not found |

**Complete Error Taxonomy:**

#### 1. EOL Framework Errors
**Error Code:** `EOL_FRAMEWORK`
**Trigger:** User specifies end-of-life framework (net452, net6.0, netcoreapp3.1, etc.)
```
{
  "success": false,
  "errorCode": "EOL_FRAMEWORK",
  "category": "ValidationError",
  "error": "Unsupported framework: .NET Framework 4.5.2 reached end-of-life on April 26, 2022. This version is not supported due to security risks and maintenance burden.",
  "suggestedFramework": "net462",
  "workaround": "Specify 'net462' (C# 7.3) as targetFramework parameter and manually verify generated code compatibility.",
  "frameworkInfo": {
    "requested": "net452",
    "isEOL": true,
    "eolDate": "2022-04-26"
  },
  "help": "Use the 'list_supported_frameworks' tool to see all supported framework monikers."
}
```

#### 2. Invalid Format Errors
**Error Code:** `INVALID_TFM_FORMAT`
**Trigger:** Malformed TFM (netfx5.0, dotnet8, framework48, etc.)
```
{
  "success": false,
  "errorCode": "INVALID_TFM_FORMAT",
  "category": "ValidationError",
  "error": "Invalid framework moniker: 'netfx5.0'. Must be valid TFM like 'net8.0', 'net48', 'netstandard2.0'.",
  "suggestedFramework": null,
  "validExamples": ["net8.0", "net48", "net462", "netstandard2.0"],
  "help": "Use the 'list_supported_frameworks' tool to see all valid framework monikers and accepted formats."
}
```

#### 3. Empty/Null Parameter Errors
**Error Code:** `MISSING_PARAMETER`
**Trigger:** Missing or empty targetFramework parameter
```
{
  "success": false,
  "errorCode": "MISSING_PARAMETER",
  "category": "ValidationError",
  "error": "Missing required parameter: 'targetFramework'. Specify the target .NET framework moniker (e.g., 'net8.0', 'net48').",
  "parameterName": "targetFramework",
  "help": "Use the 'list_supported_frameworks' tool to see all supported framework monikers."
}
```

#### 4. Unknown Framework Errors
**Error Code:** `UNKNOWN_FRAMEWORK`
**Trigger:** Valid format but unrecognized version (net10.0, net99.0, etc.)
```
{
  "success": false,
  "errorCode": "UNKNOWN_FRAMEWORK",
  "category": "ValidationError",
  "error": "Unrecognized framework: 'net10.0'. Supported frameworks: .NET 8-9, .NET Framework 4.6.2-4.8.1, .NET Standard 2.0-2.1.",
  "suggestedFramework": "net9.0",
  "supportedFrameworks": ["net9.0", "net8.0", "net481", "net48", ...],
  "help": "Use the 'list_supported_frameworks' tool for the complete list of supported frameworks with support dates."
}
```

#### 5. Refactoring Execution Errors
**Error Code:** `REFACTORING_FAILED` (or more specific codes like `NO_METHOD_FOUND`, `SYNTAX_ERROR`)
**Trigger:** Framework validation succeeds but refactoring fails
```
{
  "success": false,
  "errorCode": "NO_METHOD_FOUND",
  "category": "ExecutionError",
  "error": "Refactoring failed: No method found containing lines 100-200.",
  "frameworkInfo": {
    "targetFramework": "net8.0",
    "languageVersion": "CSharp12"
  }
}
```

**Validation Strategy:**

1. **Fail Fast with Guidance** - Reject invalid input immediately with actionable error
2. **Guide to Discovery Tool** - Every validation error includes `help` field pointing to `list_supported_frameworks`
3. **Progressive Error Detail**:
   - Invalid format → Show examples + discovery tool
   - Unknown version → Show supported list + discovery tool
   - EOL framework → Show workaround + suggested framework
4. **Self-Service First** - Agents can call `list_supported_frameworks` proactively to avoid errors
5. **Human-in-Loop** - Clear messages allow agents to ask humans for framework version

**Tool Behavior:**
- **Rejects** EOL and invalid frameworks with clear error message
- **Always includes** `help` field directing to `list_supported_frameworks` tool
- Provides workaround guidance for EOL frameworks (use nearest supported version)
- Suggests nearest supported framework when EOL detected
- Lists valid examples for format errors
- Does NOT automatically fallback or assume frameworks
- Returns errors in standardized JSON format for client handling

See [SDD Section 7](SDD-Framework-Version-Awareness.md#7-error-handling) for implementation details.

### 3.3 New Components

**Three core components enable framework-aware refactoring:**

#### Component 1: FrameworkValidator
**Purpose:** Validates framework monikers and detects EOL/invalid frameworks

**Key Responsibilities:**
- Validate TFM format (e.g., "net8.0", "net48", "netstandard2.0")
- Detect Microsoft-supported vs EOL frameworks
- Normalize framework monikers (e.g., "v4.8" → "net48")
- Provide actionable error messages with workarounds
- Suggest nearest supported framework for EOL versions

**Public Interface:**
```
Validate(targetFramework) → ValidationResult
IsSupportedFramework(targetFramework) → boolean
IsEOLFramework(targetFramework) → boolean
GetSuggestedFramework(eolFramework) → string
NormalizeMoniker(targetFramework) → string
```

#### Component 2: LanguageVersionMapper
**Purpose:** Maps framework monikers to C# language versions

**Key Responsibilities:**
- Maintain framework → C# version mapping
- Provide framework metadata (display name, support status, EOL date)
- Handle version lookups for Roslyn configuration

**Public Interface:**
```
GetLanguageVersion(targetFramework) → LanguageVersion
GetLanguageVersion(frameworkInfo) → LanguageVersion
GetFrameworkInfo(targetFramework) → FrameworkInfo
```

#### Component 3: CompilationContextBuilder
**Purpose:** Creates framework-aware Roslyn compilation contexts

**Key Responsibilities:**
- Configure C# parse options with correct language version
- Build compilation with framework-appropriate references
- Create semantic models for accurate code analysis

**Public Interface:**
```
CreateParseOptions(frameworkInfo) → CSharpParseOptions
CreateCompilation(syntaxTree, frameworkInfo) → CSharpCompilation
CreateSemanticModel(syntaxTree, frameworkInfo) → SemanticModel
```

See [SDD Section 4](SDD-Framework-Version-Awareness.md#4-component-architecture) for implementation details.

### 3.4 New Discovery Tool: List Supported Frameworks

**New MCP Tool for Framework Discovery:**

#### list_supported_frameworks Tool
```
Tool: list_supported_frameworks
Parameters: (none)

Response:
{
  "supportedFrameworks": [
    {
      "tfm": "net9.0",
      "displayName": ".NET 9",
      "languageVersion": "C# 13",
      "family": "Modern",
      "supportStatus": "Supported until Nov 2026 (STS)"
    },
    {
      "tfm": "net8.0",
      "displayName": ".NET 8",
      "languageVersion": "C# 12",
      "family": "Modern",
      "supportStatus": "Supported until Nov 2026 (LTS)"
    },
    {
      "tfm": "net48",
      "displayName": ".NET Framework 4.8",
      "languageVersion": "C# 7.3",
      "family": "Framework",
      "supportStatus": "Supported (tied to Windows lifecycle)"
    },
    // ... all supported frameworks
  ],
  "acceptedFormats": [
    "Standard TFM format (e.g., 'net8.0', 'net48', 'netstandard2.0')",
    "Alternative formats normalized: 'v4.8' → 'net48'",
    "Alternative formats normalized: '.NETFramework,Version=v4.8' → 'net48'"
  ],
  "rejectedFormats": [
    "Old-style versions: '.NET 8', 'dotnet8' (use 'net8.0')",
    "Invalid prefixes: 'netfx5.0', 'framework48' (use proper TFM)",
    "EOL frameworks: 'net6.0', 'net452' (use nearest supported version)"
  ]
}
```

**Purpose:**
- Helps agents/users discover valid framework monikers
- Shows exactly what formats are accepted
- Provides current support status for each framework
- Prevents trial-and-error with invalid formats

**Use Case:**
- Agent asks: "What .NET frameworks does this tool support?"
- User confused by TFM format validation error
- Integration testing to verify available frameworks

### 3.5 Updated MCP Tool Signatures

**All refactoring tools require a `targetFramework` parameter (v1.0.0):**

#### Extract Method Tool Signature
```
Tool: extract_method
Parameters:
  - sourceCode: string (required) - Complete C# source code
  - startLine: integer (required) - Starting line number (1-based)
  - endLine: integer (required) - Ending line number (1-based)
  - newMethodName: string (required) - Name for extracted method
  - targetFramework: string (required) - TFM (e.g., "net8.0", "net48", "net462")

Processing Flow:
  1. Validate targetFramework → reject if EOL/invalid
  2. Get framework metadata (C# version, display name)
  3. Configure Roslyn with correct language version
  4. Execute refactoring with framework-aware parsing
  5. Return refactored code + framework info
```

#### Constructor Injection Tool Signature
```
Tool: constructor_injection
Parameters:
  - sourceCode: string (required)
  - className: string (required)
  - methodName: string (required)
  - parameterNames: string (required) - Comma-separated
  - targetFramework: string (required) - TFM
  - useProperties: boolean (optional, default: false)
```

#### All Other Tools
Updated signatures for:
- **make_field_readonly** → add required `targetFramework`
- **safe_delete_method** → add required `targetFramework`
- **extract_class** → add required `targetFramework`

**Accepted TFM Formats:**
- Modern .NET: `"net8.0"`, `"net9.0"`
- .NET Framework: `"net48"`, `"net472"`, `"net462"`, `"net35"`
- .NET Standard: `"netstandard2.0"`, `"netstandard2.1"`

**Alternative Formats** (normalized internally):
- `"v4.8"` → `"net48"`
- `.NETFramework,Version=v4.8` → `"net48"`

See [SDD Section 5](SDD-Framework-Version-Awareness.md#5-mcp-tool-signature-updates) for complete C# method signatures.

### 3.6 AI Agent Integration Pattern

**This section addresses how AI agents (primary users) discover and interact with framework-aware refactoring tools.**

#### How Agents Discover Tool Requirements

**1. Tool Discovery via JSON Schema**

When an AI agent (Claude Code, GitHub Copilot, etc.) connects to RefactorCsharpMCP:

```
Agent → Server: tools/list request
Server → Agent: JSON Schema for each tool
```

**Auto-Generated Schema Example:**
```json
{
  "name": "extract_method",
  "description": "Extracts a block of code into a new private method with framework-aware syntax.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": {
        "type": "string",
        "description": "The complete C# source code"
      },
      "startLine": {
        "type": "integer",
        "description": "The starting line number (1-based) to extract"
      },
      "endLine": {
        "type": "integer",
        "description": "The ending line number (1-based) to extract"
      },
      "newMethodName": {
        "type": "string",
        "description": "The name for the new method"
      },
      "targetFramework": {
        "type": "string",
        "description": "Target framework moniker (e.g., 'net8.0', 'net48', 'net462', 'netstandard2.0')"
      }
    },
    "required": ["sourceCode", "startLine", "endLine", "newMethodName", "targetFramework"]
  }
}
```

**Key Points:**
- `targetFramework` appears in `properties` object
- `targetFramework` appears in `required` array → agents know it's mandatory
- `description` field guides agents on valid values
- Schema auto-generated from C# `[Description]` attributes

**2. Schema Generation Mechanism**

RefactorCsharpMCP uses ModelContextProtocol SDK (v0.4.0-preview.1):

```csharp
// C# Tool Definition
[McpServerTool]
[Description("Extracts a block of code...")]
public Task<object> ExtractMethod(
    [Description("The complete C# source code")] string sourceCode,
    [Description("Starting line (1-based)")] int startLine,
    [Description("Ending line (1-based)")] int endLine,
    [Description("Name for new method")] string newMethodName,
    [Description("Target framework moniker")] string targetFramework)
```

↓ **Automatically converts to JSON Schema** ↓

**Optional vs Required Parameters:**
- No default value → appears in `required` array
- Has default value (e.g., `bool useProperties = false`) → optional

#### Schema Validation

**Client-Side (Agent):**
1. Agent receives JSON Schema via `tools/list`
2. Agent validates parameters against schema **before** calling tool
3. Type mismatches caught immediately
4. Missing required parameters detected before network call

**Server-Side (RefactorCsharpMCP):**
1. MCP SDK validates incoming request against schema
2. Type coercion and validation automatic
3. Invalid requests rejected with schema violation errors
4. Custom validation in tool implementation (TFM format check)

#### Error Handling in Agent Context

Agents receive structured error responses:

```json
{
  "success": false,
  "errorCode": "MISSING_PARAMETER",
  "category": "ValidationError",
  "error": "Missing required parameter: 'targetFramework'...",
  "parameterName": "targetFramework",
  "help": "Use 'list_supported_frameworks' tool..."
}
```

**Agent Error Handling Flow:**
```
1. Agent attempts call with missing/invalid parameter
2. Receives error with errorCode and help field
3. Agent interprets errorCode:
   - MISSING_PARAMETER → Prompt human for framework
   - INVALID_TFM_FORMAT → Call list_supported_frameworks
   - EOL_FRAMEWORK → Show workaround
4. Human provides correct value
5. Agent retries with corrected parameter
```

#### Self-Service Discovery Pattern

**Agent discovers valid values via discovery tool:**

```
Agent → Server: list_supported_frameworks (no parameters)
Server → Agent: Complete list of valid TFMs with metadata
Agent → Human: "I need your .NET framework. Supported: net9.0, net8.0, net48..."
Human → Agent: "We use .NET 8"
Agent: Calls extract_method with targetFramework="net8.0"
```

#### Agent Adaptation to Schema Changes

**How agents adapt without version flags:**

1. Agent calls `tools/list` on every connection
2. Receives current schema with `targetFramework` in `required`
3. Agent detects new required parameter
4. Agent adjusts behavior:
   - Prompts human for framework version
   - Or calls `list_supported_frameworks`
   - Or reads .csproj file to detect framework
5. Schema is source of truth - no version flag needed

#### Integration Best Practices

**For Agent Developers:**

1. **Call list_supported_frameworks First** - Proactive discovery prevents errors
2. **Handle All Error Codes** - Programmatic error handling by errorCode
3. **Cache Framework Discovery** - list_supported_frameworks response cacheable per session
4. **Provide Context to Humans** - Don't just ask "What's your framework?" - explain why
5. **Validate Before Calling** - Use JSON Schema for client-side validation

#### Example Agent Interaction

```
User: "Extract lines 10-20 into ProcessData method"

Agent: [Calls tools/list, sees targetFramework required]
Agent: [Calls list_supported_frameworks]
Agent → Human: "I need your .NET framework version for compatible code.
                Your project appears to use .NET 8. Use 'net8.0'?"
Human: "Yes"

Agent → Server: extract_method(..., targetFramework="net8.0")
Server → Agent: {success: true, refactoredCode: "...", frameworkInfo: {...}}

Agent → Human: "Extracted ProcessData() using .NET 8 (C# 12) syntax."
```

See [SDD Section 5](SDD-Framework-Version-Awareness.md#5-mcp-tool-signature-updates) for implementation details.

### 3.7 Data Models (Conceptual)

**FrameworkInfo** - Represents complete framework metadata:
- Target framework moniker (e.g., "net8.0", "net462")
- Framework family (Framework, Modern, Standard)
- Version information (e.g., 4.6.2, 8.0)
- C# language version (e.g., CSharp7_3, CSharp12)
- Support status (boolean)

**FrameworkFamily** - Categorizes .NET frameworks:
- **Framework**: .NET Framework (3.5, 4.6.2-4.8.1)
- **Modern**: .NET 8-9
- **Standard**: .NET Standard 2.0-2.1

**FrameworkValidationResult** - Validation response structure:
- IsValid: Whether TFM format is correct
- IsSupported: Whether Microsoft currently supports this framework
- IsEOL: Whether this is end-of-life
- ErrorMessage: Human-readable error
- SuggestedFramework: Nearest supported version (for EOL)
- FrameworkInfo: Complete metadata (if valid)

See [SDD Section 2](SDD-Framework-Version-Awareness.md#2-data-models) for complete C# class definitions.

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
- Add required `targetFramework` parameter to all 5 refactoring MCP tools
- Implement `list_supported_frameworks` discovery tool (no parameters)
- Update MCP tool error responses for invalid/EOL frameworks
- Include framework info in success responses
- Update tool descriptions in MCP metadata
- End-to-end MCP tests (10 tests: 8 refactoring + 2 discovery)

**Initial Release (v1.0.0):**
- This is the **initial release** with framework-aware refactoring
- All MCP tools require `targetFramework` parameter from the start
- No migration needed - this is the first production release

### Phase 4: Testing & Documentation (Week 4)
**Deliverables:**
- Complete test suite (45+ new tests total)
- FRAMEWORK-SUPPORT.md documentation
- Updated README.md, EXAMPLES.md
- TROUBLESHOOTING.md updates

### Phase 5: DevTools Validation & Release (Week 5)
**Deliverables:**
- Test BackupTool refactoring with `targetFramework="net462"`
- Test passgen refactoring with `targetFramework="net8.0"`
- Verify EOL framework rejection for net452
- Real-world examples for all supported frameworks
- Performance benchmarks
- Tag and release v1.0.0

**v1.0.0 Release (End of Week 5):**
- Initial production release with framework-aware refactoring
- All MCP tools include required `targetFramework` parameter
- Publish to MCP catalog with framework support documentation
- Announce on GitHub Discussions

**User Onboarding:**
- Clear documentation showing `targetFramework` is required
- `list_supported_frameworks` tool helps discover valid values
- Examples for all supported frameworks
- Error messages guide users to correct usage

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
