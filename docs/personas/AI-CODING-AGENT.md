# User Persona: AI Coding Agent

**Version:** 1.0.0
**Date:** 2025-10-09
**Archetype:** AI-Powered Development Assistant

---

## Overview

**Name:** Claude Code (Representative AI Agent)
**Type:** Model Context Protocol (MCP) Client
**Role:** AI Development Assistant
**Organization:** Anthropic / Third-party MCP integrations
**Human Partners:** Sarah (Full-Stack Dev), Mike (Legacy Maintainer), and thousands of developers

---

## Background & Context

### What is an AI Coding Agent?
An AI coding agent is a software assistant that helps developers write, refactor, and understand code. RefactorCsharpMCP is designed to be consumed primarily by AI agents through the Model Context Protocol (MCP), not directly by human developers.

**Key AI Agents using MCP:**
- **Claude Code** (Anthropic's official CLI)
- **Cursor** (AI-powered code editor)
- **Continue** (VS Code extension)
- **Custom MCP integrations** (community-built tools)

### Technical Profile
- **Protocol:** Model Context Protocol (MCP) stdio transport
- **Interface:** JSON-RPC over stdin/stdout
- **Discovery:** Dynamic tool discovery via `tools/list`
- **Validation:** JSON Schema-based parameter validation
- **Error Handling:** Structured error responses with error codes

### Operational Environment
- **Runtime:** Spawned as subprocess by AI client (e.g., Claude Code)
- **Lifecycle:** Started on-demand, may be kept alive for session
- **State:** Stateless - each refactoring request is independent
- **Concurrency:** May handle multiple requests from different AI clients

---

## Goals & Motivations

### Primary Goals
1. **Assist Human Developers:** Enable humans to refactor code through natural language
2. **Provide Reliable Tools:** Offer deterministic, correct refactoring operations
3. **Handle Ambiguity:** Interpret human requests and map to appropriate tool calls
4. **Validate Parameters:** Ensure tool calls are well-formed before execution
5. **Explain Results:** Translate technical responses into human-understandable language

### What Success Looks Like
- ✅ Human asks "Extract this method" → Agent calls correct MCP tool with correct parameters
- ✅ Tool returns refactored code → Agent presents it clearly to human
- ✅ Tool returns error → Agent explains error and suggests fix
- ✅ Human provides framework version → Agent includes in tool call
- ✅ Tool executes in <2 seconds → Human maintains flow state

---

## Interaction Model

### Agent Workflow (Typical Refactoring Request)

#### 1. Human Input (Natural Language)
```
Human: "Extract lines 15-25 into a method called ValidateInput.
        This is a .NET 8 project."
```

#### 2. Agent Interpretation
Agent parses natural language to identify:
- **Tool:** `extract_method`
- **Parameters:**
  - sourceCode: (full file content from context)
  - startLine: 15
  - endLine: 25
  - newMethodName: "ValidateInput"
  - targetFramework: "net8.0" (inferred from ".NET 8 project")

#### 3. Parameter Validation (Client-Side)
Agent validates against JSON Schema:
- All required parameters present? ✅
- Parameter types correct? ✅
- Framework format valid? ✅ (checks format, not EOL status)

#### 4. Tool Invocation
```json
{
  "method": "tools/call",
  "params": {
    "name": "extract_method",
    "arguments": {
      "sourceCode": "...",
      "startLine": 15,
      "endLine": 25,
      "newMethodName": "ValidateInput",
      "targetFramework": "net8.0"
    }
  }
}
```

#### 5. Response Handling

**Success Case:**
```json
{
  "content": [
    {
      "type": "text",
      "text": "{\"success\":true,\"refactoredCode\":\"...\",\"frameworkInfo\":{...}}"
    }
  ]
}
```

Agent presents to human:
```
I've extracted ValidateInput() from lines 15-25. The method was generated
using .NET 8 (C# 12) syntax. Here's the refactored code:

[Shows refactored code]

Would you like me to explain the changes or make any adjustments?
```

**Error Case:**
```json
{
  "success": false,
  "errorCode": "EOL_FRAMEWORK",
  "error": "Unsupported framework: .NET 6 reached end-of-life...",
  "suggestedFramework": "net8.0"
}
```

Agent presents to human:
```
I couldn't perform the refactoring because .NET 6 is no longer supported
(reached end-of-life in November 2024).

I recommend using .NET 8 instead. Would you like me to:
1. Refactor using "net8.0" (recommended)
2. Check which .NET versions are supported (list_supported_frameworks)
```

---

## Capabilities & Limitations

### What the Agent Can Do

#### 1. Natural Language Understanding
- Parse refactoring requests from human language
- Infer missing parameters from context (e.g., project framework)
- Handle ambiguity by asking clarifying questions
- Recognize synonyms ("extract", "pull out", "separate into")

#### 2. Context Management
- Remember framework version for session (reduce repeated questions)
- Track file content across conversation
- Understand line numbers from displayed code
- Access project files to determine framework

#### 3. Error Recovery
- Interpret error codes and explain to human
- Suggest corrections for invalid parameters
- Guide human through tool discovery (`list_supported_frameworks`)
- Retry with corrected parameters

#### 4. Multi-Step Operations
- Chain multiple refactorings sequentially
- Validate each step before proceeding
- Rollback if step fails
- Present cumulative changes

### What the Agent Cannot Do

#### 1. Semantic Understanding
- Cannot understand "business logic" vs "validation logic" without human guidance
- Cannot determine "good" vs "bad" method names
- Cannot judge whether a refactoring improves code quality

#### 2. Cross-File Awareness
- Limited to single file refactorings (V1 limitation)
- Cannot detect cross-file references
- Cannot update callers in other files

#### 3. Framework Auto-Detection
- Cannot automatically detect framework version (requires human or project file)
- Cannot validate EOL status before server call (relies on server validation)
- Cannot upgrade code to newer frameworks automatically

#### 4. Custom Business Rules
- Cannot enforce company-specific coding standards
- Cannot apply custom refactoring patterns
- Cannot integrate with custom CI/CD pipelines

---

## Tool Discovery & Parameter Handling

### Dynamic Tool Discovery

**On Connection:**
```
Agent → Server: tools/list
Server → Agent: [extract_method, constructor_injection, make_field_readonly,
                 safe_delete_method, extract_class, list_supported_frameworks]
```

**Schema for extract_method:**
```json
{
  "name": "extract_method",
  "description": "Extracts a block of code into a new private method with framework-aware syntax.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "sourceCode": {"type": "string", "description": "The complete C# source code"},
      "startLine": {"type": "integer", "description": "The starting line number (1-based)"},
      "endLine": {"type": "integer", "description": "The ending line number (1-based)"},
      "newMethodName": {"type": "string", "description": "The name for the new method"},
      "targetFramework": {
        "type": "string",
        "description": "Target framework moniker (e.g., 'net8.0', 'net48', 'net462')"
      }
    },
    "required": ["sourceCode", "startLine", "endLine", "newMethodName", "targetFramework"]
  }
}
```

### Parameter Resolution Strategy

#### Case 1: Human Provides Framework Explicitly
```
Human: "Extract this method using .NET Framework 4.8"
Agent: ✅ Maps ".NET Framework 4.8" → "net48"
```

#### Case 2: Agent Infers from Project Context
```
Human: "Extract this method"
Agent: [Reads .csproj file]
Agent: Finds <TargetFramework>net8.0</TargetFramework>
Agent: ✅ Uses "net8.0" in tool call
```

#### Case 3: Agent Asks Human
```
Human: "Extract this method"
Agent: [No project context available]
Agent: "I need to know your .NET framework version for compatible code.
        What version is this project using? (e.g., .NET 8, .NET Framework 4.8)"
Human: ".NET 8"
Agent: ✅ Uses "net8.0" in tool call
```

#### Case 4: Agent Uses Discovery Tool
```
Human: "What .NET versions do you support?"
Agent → Server: list_supported_frameworks
Server → Agent: [net9.0, net8.0, net48, net472, net462, ...]
Agent: "I support .NET 8-9, .NET Framework 4.6.2-4.8.1, and .NET Standard 2.0-2.1."
```

---

## Error Handling & Recovery

### Error Categories Agent Handles

#### 1. Validation Errors (Client-Side)
**Detected before server call:**
- Missing required parameter
- Invalid parameter type (string instead of integer)
- Malformed JSON

**Agent Response:**
```
Agent → Human: "I need a starting line number for the extraction.
                Which line should I start from?"
```

#### 2. Framework Errors (Server-Side)

**EOL Framework:**
```
Error Code: EOL_FRAMEWORK
Agent → Human: "This project uses .NET 6, which reached end-of-life in
                November 2024. I recommend upgrading to .NET 8 for security
                and support. Should I use .NET 8 syntax for the refactoring?"
```

**Invalid TFM Format:**
```
Error Code: INVALID_TFM_FORMAT
Agent → Human: "I couldn't recognize the framework format 'dotnet8'.
                Did you mean 'net8.0'? Let me check the supported formats..."
[Calls list_supported_frameworks]
```

**Unknown Framework:**
```
Error Code: UNKNOWN_FRAMEWORK
Agent → Human: "I don't recognize .NET 10. The latest supported version is
                .NET 9. Would you like to see all supported versions?"
```

#### 3. Refactoring Errors (Server-Side)

**No Method Found:**
```
Error Code: NO_METHOD_FOUND
Agent → Human: "I couldn't find a method containing lines 100-200.
                Let me show you the file structure. Can you verify the line numbers?"
```

**Syntax Error:**
```
Error Code: SYNTAX_ERROR
Agent → Human: "The source code has syntax errors that prevent refactoring.
                Let me help you fix the syntax first."
```

**Compilation Error:**
```
Error Code: REFACTORING_FAILED
Agent → Human: "The refactoring would cause compilation errors. This might be
                because [explanation from error message]. Should we try a
                different approach?"
```

---

## Trust & Verification

### How Agent Builds Trust with Human

#### 1. Explanation of Changes
```
Agent: "I've extracted ValidateInput() with these changes:
        - Created new private method ValidateInput(Order order)
        - Detected 1 parameter (order) via data flow analysis
        - Detected 1 return value (bool isValid)
        - Updated 3 call sites to use new method
        Would you like to see the full diff?"
```

#### 2. Framework Awareness Communication
```
Agent: "I used .NET 8 (C# 12) syntax for this refactoring, which allows:
        - Collection expressions
        - Primary constructors
        - Required members
        The refactored code is compatible with your project's framework."
```

#### 3. Limitation Disclosure
```
Agent: "Note: Safe Delete only checks references within this file.
        You should verify there are no references in other files before
        deleting. Would you like me to search the solution?"
```

#### 4. Confidence Indicators
```
Agent: "✅ Refactoring successful - code compiles
        ✅ Used correct framework (.NET 8 / C# 12)
        ⚠️  Warning: 1 manual step required (see below)
        ℹ️  Tip: Run your test suite to verify behavior unchanged"
```

---

## Performance & Reliability

### Performance Expectations
- **Tool Discovery:** <100ms (cached after first call)
- **Simple Refactoring:** <500ms (Extract Method, Rename)
- **Complex Refactoring:** <2 seconds (Extract Class, Inline Method)
- **Timeout:** Agent waits up to 10 seconds before reporting timeout

### Reliability Requirements
- **Success Rate:** >95% for common cases
- **Error Rate:** <5% (errors due to invalid input, not tool bugs)
- **Availability:** 99.9% uptime (stdio transport, local process)
- **Determinism:** Same input → same output (no randomness)

### Failure Modes

#### 1. Tool Crashes
```
Agent detects: Process exited unexpectedly
Agent → Human: "The refactoring tool crashed. This is unusual.
                Would you like me to retry or report this issue?"
```

#### 2. Timeout
```
Agent detects: No response after 10 seconds
Agent → Human: "The refactoring is taking longer than expected.
                This might be due to a very large file. Should I wait longer
                or cancel?"
```

#### 3. Invalid Output
```
Agent detects: Malformed JSON response
Agent → Human: "I received an unexpected response from the tool.
                This might be a bug. Let me try a simpler refactoring first."
```

---

## Learning & Adaptation

### How Agent Improves Over Time

#### 1. Pattern Recognition
Agent learns common human patterns:
- "Extract this" → probably Extract Method
- "Make this injectable" → probably Constructor Injection
- "Clean up this class" → multiple refactorings

#### 2. Context Memory (Session)
Agent remembers within conversation:
- Framework version (ask once, use for all refactorings)
- Coding preferences (fields vs properties for DI)
- File locations and structure

#### 3. Error Recovery
Agent learns from errors:
- Human corrects line numbers → remember to ask for verification
- Framework error → remember to ask framework earlier next time
- Timeout on large file → warn about file size before refactoring

---

## Integration Patterns

### Best Practices for Agent-Tool Integration

#### 1. Proactive Framework Discovery
```
Agent Strategy:
1. On first refactoring request, call list_supported_frameworks
2. Cache supported frameworks for session
3. When human mentions framework, validate against cached list
4. If invalid, suggest nearest supported framework
```

#### 2. Context-Aware Parameter Resolution
```
Agent Strategy:
1. Check .csproj for <TargetFramework>
2. If not found, check conversation history for framework mention
3. If not found, ask human once and cache for session
4. Include framework in all subsequent tool calls
```

#### 3. Error-Driven Clarification
```
Agent Strategy:
1. Attempt refactoring with inferred parameters
2. If error, parse errorCode and category
3. Present error in human language with suggested fix
4. Ask clarifying question to resolve
5. Retry with corrected parameters
```

#### 4. Multi-Step Refactoring
```
Agent Strategy:
1. Human asks for multiple refactorings
2. Agent sequences tool calls
3. After each step, validate compilation
4. If step fails, stop sequence and report
5. Explain what succeeded and what failed
```

---

## Success Criteria for RefactorCsharpMCP

### Must-Have (From Agent Perspective)

#### 1. Clear Tool Descriptions
- JSON Schema descriptions must be unambiguous
- Tool names must be self-explanatory
- Parameter names must match human language

#### 2. Structured Error Responses
- Error codes for programmatic handling
- Human-readable error messages
- Suggested fixes when possible
- Help text pointing to discovery tools

#### 3. Framework Support Discovery
- `list_supported_frameworks` tool for self-service
- Accepted TFM formats documented
- EOL frameworks clearly marked
- Suggested alternatives provided

#### 4. Deterministic Behavior
- Same input → same output
- No hidden state between calls
- No side effects (file system changes, etc.)
- Stateless operation

#### 5. Performance
- <2 seconds for typical refactorings
- <100ms tool discovery
- Timeout after 10 seconds (agent can retry)

### Nice-to-Have

#### 1. Detailed Change Information
- List of changes made (fields added, methods extracted, etc.)
- Line numbers affected
- Framework/language features used
- Warnings about manual steps required

#### 2. Dry-Run Mode
- Preview refactoring without applying
- Show diff before committing
- Validate compilation without modifying source

#### 3. Batch Operations
- Refactor multiple selections in one call
- Atomic transactions (all succeed or all fail)
- Progress reporting for long operations

---

## Quotes

> "My job is to make RefactorCsharpMCP accessible to humans who speak English, not JSON."

> "If the tool gives me an error code, I can handle it programmatically. If it gives me a stack trace, I'm guessing."

> "Framework awareness is non-negotiable. Generating C# 12 code for a .NET Framework 4.8 project breaks trust instantly."

> "I need to know what the tool CAN'T do so I can set correct human expectations."

> "The best MCP tool is one I never have to explain to the human. It just works."

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-10-09 | Initial persona based on PRD v1.1.0 and Framework Awareness PRD |

---

**Persona Owner:** Product Owner (Master)
**Last Review:** 2025-10-09
**Next Review:** After V1 release integration feedback
