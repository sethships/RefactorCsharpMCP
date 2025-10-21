# User Persona: Sarah - Full-Stack Developer

**Version:** 1.0.0
**Date:** 2025-10-09
**Archetype:** AI-Assisted Full-Stack Developer

---

## Overview

**Name:** Sarah Martinez
**Age:** 32
**Role:** Senior Full-Stack Developer
**Organization:** Mid-sized SaaS company (100-500 employees)
**Years of Experience:** 8 years professional development, 3 years with C#/.NET

---

## Background & Experience

### Technical Background
- **Primary Stack:** C# backend (ASP.NET Core, Web API), React frontend
- **Current Projects:** Microservices architecture on .NET 8
- **Framework Experience:**
  - .NET 8 (current primary)
  - .NET Framework 4.8 (legacy maintenance)
  - Some exposure to .NET 6/7 (previous projects)
- **Development Environment:**
  - VS Code with C# Dev Kit
  - Claude Code for AI-assisted development
  - GitHub Copilot (occasional use)
  - Docker for local development

### Experience Level
- **C# Proficiency:** Intermediate to Advanced
- **Refactoring Knowledge:** Good understanding of common patterns
- **AI-Assisted Development:** Early adopter (1.5 years using Claude Code)
- **Architecture Awareness:** Strong understanding of clean architecture, SOLID principles

### Daily Workflow
Sarah starts her day by reviewing pull requests, then works on new features or refactoring technical debt. She uses Claude Code to:
- Extract methods when creating new features
- Refactor long methods into smaller, testable units
- Apply dependency injection patterns
- Clean up code before submitting PRs

---

## Goals & Motivations

### Primary Goals
1. **Ship Features Quickly:** Deliver high-quality code without getting bogged down in manual refactoring
2. **Improve Code Quality:** Reduce technical debt while maintaining velocity
3. **Enable Testing:** Make code testable through proper dependency injection
4. **Team Consistency:** Ensure refactorings follow team conventions

### What Success Looks Like for Sarah
- ✅ Extract a 50-line method into 3-4 focused methods in under 2 minutes
- ✅ Convert static dependencies to DI without breaking existing tests
- ✅ Refactor code that compiles on first try (no manual fixes needed)
- ✅ Code passes team's PR review without refactoring-related comments
- ✅ Refactored code uses modern C# 12 idioms (her team's standard)

### Key Performance Indicators
- **Velocity:** Maintain 80%+ story point completion rate
- **Quality:** Keep PR revision count under 2 per feature
- **Technical Debt:** Dedicate 20% of sprint to refactoring
- **Test Coverage:** Maintain 85%+ coverage on new code

---

## Pain Points & Challenges

### Current Frustrations
1. **Manual Refactoring is Tedious**
   - Extracting methods requires careful parameter detection
   - Updating all call sites after extraction is error-prone
   - Constructor injection requires boilerplate (field, constructor param, assignment)

2. **Context Switching**
   - Moving between IDE refactoring tools and AI chat disrupts flow
   - VS Code refactoring tools less sophisticated than Visual Studio
   - Manual verification needed after automated refactorings

3. **Team Code Review Delays**
   - Large PRs with refactoring + new features slow review
   - Reviewers request refactorings that require rework
   - Inconsistent naming conventions across team

4. **Legacy Code Maintenance**
   - Team has 2-3 legacy .NET Framework 4.8 services
   - Different refactoring patterns needed for Framework vs .NET 8
   - Fear of breaking legacy code when refactoring

### Specific Challenges with RefactorCsharpMCP
- **Learning Curve:** Needs clear examples to understand tool capabilities
- **Trust:** Must verify refactorings compile and pass tests
- **Integration:** Wants seamless Claude Code integration (already has this)
- **Framework Awareness:** Needs tool to respect .NET 8 vs .NET Framework differences

---

## How Sarah Uses RefactorCsharpMCP

### Typical Usage Scenarios

#### Scenario 1: Feature Development with Extract Method
**Context:** Building a new order processing feature, method grows to 60 lines

**Workflow:**
1. Sarah writes feature code in a single method (quick iteration)
2. Runs unit tests to verify logic
3. Asks Claude Code: "Extract lines 15-25 into a method called ValidateOrderData"
4. Claude Code calls `extract_method` with `targetFramework="net8.0"`
5. Reviews refactored code, commits

**Value Delivered:**
- 90 seconds vs 5-10 minutes manual extraction
- Correct parameter detection (no missed dependencies)
- Maintains C# 12 modern syntax
- Compiles on first try

#### Scenario 2: Applying Dependency Injection
**Context:** Refactoring a service to support unit testing

**Workflow:**
1. Identifies method using `new HttpClient()` and `new Logger()`
2. Asks Claude Code: "Convert logger and httpClient to constructor injection"
3. Claude Code calls `constructor_injection` with field creation
4. Verifies constructor parameters and field usage
5. Updates test setup to inject mocks

**Value Delivered:**
- Eliminates boilerplate (field declaration, constructor parameter, assignment)
- Consistent pattern across codebase
- Enables testing with mocked dependencies

#### Scenario 3: Code Cleanup Before PR
**Context:** Finishing feature, preparing for team review

**Workflow:**
1. Reviews code for long methods, unused usings, mutable fields
2. Asks Claude Code: "Clean up this class - extract long methods, remove unused usings, make fields readonly"
3. Claude Code performs multiple refactorings sequentially
4. Runs test suite to verify no regressions
5. Submits PR with clean, well-structured code

**Value Delivered:**
- Faster PR reviews (clean code easier to review)
- Fewer revision requests from team
- Maintains team coding standards

#### Scenario 4: Legacy Service Maintenance
**Context:** Bug fix in .NET Framework 4.8 service

**Workflow:**
1. Makes fix in legacy codebase
2. Wants to extract a method but knows code targets Framework 4.8
3. Asks Claude Code: "Extract this method (this is .NET Framework 4.8)"
4. Claude Code calls `extract_method` with `targetFramework="net48"`
5. Verifies generated code uses C# 7.3 syntax (no newer features)

**Value Delivered:**
- Framework-appropriate refactoring (no C# 12 features in Framework 4.8 code)
- Avoids compilation errors in legacy project
- Confidence that refactored code is compatible

---

## Tools & Workflows

### Development Tools
- **Primary IDE:** Visual Studio Code with C# Dev Kit
- **AI Assistant:** Claude Code (daily use)
- **Version Control:** Git via VS Code + GitHub
- **Testing:** xUnit, NSubstitute for mocking
- **CI/CD:** GitHub Actions
- **Code Quality:** SonarQube, Coverlet for coverage

### Current Refactoring Workflow
1. **Identify Code Smell:** Long method, duplicate code, god class
2. **Ask Claude Code:** Natural language request for refactoring
3. **Review Generated Code:** Verify semantics preserved
4. **Run Tests:** Ensure no regressions
5. **Commit:** Small, focused commits for each refactoring

### Integration with RefactorCsharpMCP
Sarah interacts with RefactorCsharpMCP **entirely through Claude Code**:
- Never calls MCP tools directly
- Uses natural language to request refactorings
- Claude Code determines correct tool and parameters
- Transparent MCP integration (Sarah doesn't need to know MCP details)

---

## Success Criteria for RefactorCsharpMCP

### Must-Have Capabilities
1. **Extract Method:** Most frequently used refactoring (3-5 times per day)
2. **Constructor Injection:** Critical for testability (2-3 times per week)
3. **Framework Awareness:** Must respect .NET 8 vs .NET Framework differences
4. **Compilation Guarantee:** Refactored code must compile without manual fixes

### Nice-to-Have Capabilities
1. **Rename Symbol:** Improving variable/method names
2. **Inline Variable:** Simplifying over-abstracted code
3. **Remove Unused Usings:** Cleanup before commits

### Deal-Breakers
1. ❌ **Compilation Errors:** Refactored code that doesn't compile
2. ❌ **Semantic Changes:** Altered behavior after refactoring
3. ❌ **Framework Incompatibility:** C# 12 code in .NET Framework 4.8 project
4. ❌ **Unclear Errors:** Cryptic error messages that don't explain what went wrong

---

## Quotes

> "I don't want to spend 10 minutes manually extracting a method. Claude Code + RefactorCsharpMCP lets me do it in 30 seconds."

> "The hardest part of refactoring is remembering which parameters to pass. Extract Method figures that out for me."

> "I love that it respects .NET 8 vs Framework 4.8. I was worried it would generate modern syntax in legacy code."

> "Before RefactorCsharpMCP, I'd skip refactorings because they took too long. Now I refactor as I go."

> "The best refactoring tool is one I don't have to think about. Claude Code just does it."

---

## Demographics & Context

### Team Environment
- **Team Size:** 6 developers (2 senior, 3 mid, 1 junior)
- **Code Review:** All PRs require 1 approval
- **Standards:** Team follows C# coding conventions, SOLID principles
- **Architecture:** Microservices with ASP.NET Core, RabbitMQ, PostgreSQL

### Project Characteristics
- **Active Services:** 12 microservices (8 on .NET 8, 4 on .NET Framework 4.8)
- **Codebase Size:** ~200K LOC across all services
- **Test Coverage:** 82% average, team target is 85%
- **Deployment:** Kubernetes, Docker, automated CI/CD

### Learning Style
- **Prefers:** Learning by example, hands-on experimentation
- **Documentation:** Scans examples first, reads details when stuck
- **Troubleshooting:** Tries solution, reads error message, consults docs if needed

---

## Relationship with AI-Assisted Development

### Claude Code Usage Patterns
- **Frequency:** 20-30 interactions per day
- **Primary Use Cases:**
  - Code generation (30%)
  - Refactoring (25%)
  - Debugging (20%)
  - Documentation (15%)
  - Test writing (10%)
- **Trust Level:** High - verifies output but expects correctness
- **Workflow Integration:** Embedded in daily coding flow (not occasional use)

### Expectations for AI-Assisted Refactoring
1. **Natural Language Interface:** "Extract this method" not "extract_method(line=10, ...)"
2. **Context Awareness:** Tool should know framework from project context
3. **Explanation:** Brief explanation of what changed (optional)
4. **Confidence:** Refactorings should work 95%+ of the time
5. **Incremental:** Small, focused refactorings (not massive rewrites)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-10-09 | Initial persona based on PRD v1.1.0 |

---

**Persona Owner:** Product Owner (Master)
**Last Review:** 2025-10-09
**Next Review:** After V1 release user feedback
