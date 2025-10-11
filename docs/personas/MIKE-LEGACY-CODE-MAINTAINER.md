# User Persona: Mike - Legacy Code Maintainer

**Version:** 1.0.0
**Date:** 2025-10-09
**Archetype:** Legacy Codebase Specialist

---

## Overview

**Name:** Mike Thompson
**Age:** 42
**Role:** Senior Software Engineer / Technical Lead
**Organization:** Enterprise software company (1000+ employees)
**Years of Experience:** 18 years professional development, 15 years with C#/.NET

---

## Background & Experience

### Technical Background
- **Primary Responsibility:** Maintaining large .NET Framework codebase (15+ years old)
- **Current Projects:**
  - Legacy ERP system (.NET Framework 4.6.2, 4.7.2, 4.8)
  - Gradual modernization to .NET 8 (pilot services)
  - Supporting critical production systems
- **Framework Experience:**
  - .NET Framework 3.5 through 4.8.1 (deep expertise)
  - .NET 8 (learning, pilot projects)
  - Limited exposure to .NET Core 2.x/3.x (deprecated projects)
- **Development Environment:**
  - Visual Studio 2022 (primary)
  - VS Code for quick edits
  - ReSharper (team standard)
  - Recently started using Claude Code (3 months)

### Experience Level
- **C# Proficiency:** Expert (.NET Framework), Intermediate (.NET 8)
- **Refactoring Knowledge:** Extensive - regularly refactors legacy code
- **AI-Assisted Development:** Cautious adopter (skeptical but curious)
- **Architecture Awareness:** Deep understanding of layered architecture, enterprise patterns

### Daily Workflow
Mike spends most of his time on:
- **Bug fixes:** 40% - Critical production issues in legacy systems
- **Refactoring:** 30% - Reducing technical debt, improving maintainability
- **Code Review:** 20% - Reviewing team's changes to legacy codebase
- **Modernization:** 10% - Piloting .NET 8 migration for select modules

**Typical Day:**
1. Morning: Triage production issues
2. Mid-morning: Refactor code around bug fix area
3. Afternoon: Code reviews, mentoring junior developers
4. End of day: Work on modernization pilot projects

---

## Goals & Motivations

### Primary Goals
1. **Reduce Technical Debt:** Break down god classes, extract methods from 500+ line methods
2. **Improve Maintainability:** Make legacy code easier to understand and modify
3. **Enable Testing:** Introduce testability to legacy code (mostly untested)
4. **Preserve Stability:** Never break existing functionality (risk-averse)
5. **Knowledge Transfer:** Document and simplify code for team

### What Success Looks Like for Mike
- ✅ Extract a 200-line method into 6-8 focused methods without breaking tests
- ✅ Safely delete unused code after verifying no references
- ✅ Convert tightly-coupled classes to use dependency injection
- ✅ Refactor code that compiles and passes existing tests without modification
- ✅ Reduce cyclomatic complexity in critical modules

### Key Performance Indicators
- **Stability:** Zero production incidents caused by refactoring
- **Technical Debt:** Reduce code complexity by 15% year-over-year
- **Test Coverage:** Increase coverage from 30% to 50% on legacy modules
- **Knowledge Sharing:** 2-3 refactoring workshops per quarter for team

---

## Pain Points & Challenges

### Current Frustrations

#### 1. Legacy Code Complexity
- **God Classes:** Some classes exceed 5,000 lines with 50+ methods
- **Long Methods:** Methods with 300-500 lines, deeply nested conditionals
- **Tight Coupling:** Classes depend on concrete implementations, hard to test
- **No Separation of Concerns:** Business logic, data access, UI logic all mixed
- **Minimal Test Coverage:** 30% coverage, mostly integration tests

#### 2. Refactoring Risk
- **Fear of Breaking Changes:** Legacy code lacks tests, changes are risky
- **Hidden Dependencies:** Unclear which code depends on what
- **Cross-Assembly References:** Changes affect multiple projects
- **Shared State:** Global variables, static classes, singleton abuse
- **Production Criticality:** Downtime costs $10K+ per hour

#### 3. Manual Refactoring Limitations
- **Time-Consuming:** Extracting a method from 500-line function takes 30-60 minutes
- **Error-Prone:** Manually tracking parameters, return values, variable scope
- **Incomplete:** Hard to find all references to methods/fields
- **Consistency:** Team has varying refactoring skill levels

#### 4. Framework Version Constraints
- **Stuck on .NET Framework:** Cannot use C# 8-12 features in legacy code
- **Mixed Codebases:** Some projects on 4.6.2, others on 4.8, pilot on .NET 8
- **Compiler Compatibility:** Must ensure refactored code compiles on target framework
- **Migration Challenges:** Incrementally moving to .NET 8 while maintaining legacy systems

### Specific Challenges with RefactorCsharpMCP
- **Trust Issues:** Needs extensive validation before trusting automated refactoring
- **Complex Scenarios:** Legacy code has edge cases AI tools might miss
- **Framework Awareness:** CRITICAL that tool doesn't introduce C# 12 features in Framework 4.8 code
- **Cross-File Dependencies:** Needs to understand limitations (single-file only in V1)
- **Safety Guarantees:** Must not break existing behavior

---

## How Mike Uses RefactorCsharpMCP

### Typical Usage Scenarios

#### Scenario 1: Taming a God Class
**Context:** CustomerService class has 3,500 lines, needs to extract validation logic

**Workflow:**
1. Identifies 150 lines of validation logic to extract
2. Asks Claude Code: "Extract lines 450-600 into a new class called CustomerValidator"
3. Claude Code calls `extract_class` with `targetFramework="net472"` (project TFM)
4. **Carefully reviews** generated code:
   - Verifies C# 7.3 syntax only (no C# 8+ features)
   - Checks field references updated correctly
   - Ensures no semantic changes
5. Runs **all existing tests** (even if just 5 tests)
6. Performs manual smoke testing in staging environment
7. Commits with detailed commit message

**Value Delivered:**
- 15 minutes vs 2-3 hours manual extraction
- Confidence that C# version is compatible
- Reduced fear of breaking changes
- Incremental improvement to god class

#### Scenario 2: Safe Deletion of Dead Code
**Context:** Found a method that appears unused, wants to verify before deleting

**Workflow:**
1. Suspects `CalculateLegacyDiscount()` is no longer called
2. Uses search tools to check for references (finds none)
3. Asks Claude Code: "Is CalculateLegacyDiscount safe to delete?"
4. Claude Code calls `safe_delete_method` with `targetFramework="net48"`
5. Tool reports: "No references found in file, but cross-file references not checked. Search recommended."
6. Mike performs solution-wide search for method name
7. Verifies method not called via reflection or dynamic invocation
8. Deletes method, commits with note "Safe Delete verified"

**Value Delivered:**
- Automated detection of single-file references
- Clear warning about cross-file limitation
- Confidence to remove dead code
- Reduced codebase size

#### Scenario 3: Introducing Testability
**Context:** Refactoring untested code to enable unit testing

**Workflow:**
1. Has method with `new SqlConnection()`, `new Logger()` hard-coded
2. Asks Claude Code: "Convert this method to use constructor injection for database and logger"
3. Claude Code calls `constructor_injection` with `targetFramework="net48"`
4. Reviews generated constructor, fields, updated method body
5. Verifies uses readonly fields (good practice)
6. Creates unit test with mock database and logger
7. Verifies test passes before committing

**Value Delivered:**
- Enables testing of previously untestable code
- Follows .NET Framework 4.8 patterns (no modern DI syntax)
- Incremental improvement to legacy codebase
- Reduces resistance to refactoring (safer with tests)

#### Scenario 4: Breaking Down 500-Line Method
**Context:** Critical order processing method is 500 lines, needs to be broken down

**Workflow:**
1. Analyzes method, identifies logical sections:
   - Lines 10-50: Validation
   - Lines 51-150: Pricing calculation
   - Lines 151-300: Tax computation
   - Lines 301-450: Payment processing
2. Starts conservatively: asks Claude Code to extract validation first
3. Claude Code calls `extract_method` with `targetFramework="net48"`
4. Runs tests after each extraction
5. Repeats for pricing, tax, payment sections
6. Results: 500-line method becomes 50-line orchestrator with 4 focused methods

**Value Delivered:**
- Reduced complexity from 120 to 25 cyclomatic complexity
- Easier to understand and maintain
- Each extracted method can be tested independently
- Preserves exact behavior (no semantic changes)

#### Scenario 5: Framework Version Migration
**Context:** Piloting migration of OrderService from .NET Framework 4.8 to .NET 8

**Workflow:**
1. Creates new .NET 8 project, copies code
2. Identifies areas to refactor for modernization
3. Asks Claude Code: "Refactor this method using .NET 8 best practices"
4. Claude Code calls `extract_method` with `targetFramework="net8.0"`
5. Tool uses C# 12 features (collection expressions, primary constructors)
6. Mike reviews modern C# syntax, learns new patterns
7. Asks clarifying questions about C# 12 features

**Value Delivered:**
- Framework-appropriate refactoring (modern syntax for .NET 8)
- Learning opportunity for Mike (seeing modern C# in practice)
- Accelerated migration (automated modernization)
- Confidence that refactored code matches target framework

---

## Tools & Workflows

### Development Tools
- **Primary IDE:** Visual Studio 2022 Enterprise
- **Refactoring Aid:** ReSharper (team standard, but heavy)
- **AI Assistant:** Claude Code (recent addition, cautious use)
- **Version Control:** Git via Visual Studio + Azure DevOps
- **Testing:** NUnit, Moq for mocking (limited coverage)
- **CI/CD:** Azure Pipelines
- **Code Quality:** SonarQube, manual code reviews

### Current Refactoring Workflow (Before RefactorCsharpMCP)
1. **Identify Refactoring Opportunity:** Code smell, complexity metric
2. **ReSharper Refactoring:** Use built-in tools where possible
3. **Manual Refactoring:** Complex cases done by hand (error-prone)
4. **Test Validation:** Run tests (if they exist)
5. **Manual Verification:** Smoke test in staging environment
6. **Code Review:** Peer review before merge
7. **Gradual Rollout:** Deploy to 10% of users, monitor, expand

### Integration with RefactorCsharpMCP
Mike's workflow with RefactorCsharpMCP:
- **Uses through Claude Code:** Natural language requests
- **Validation-Heavy:** Always reviews generated code carefully
- **Test-Driven:** Runs tests after every refactoring
- **Conservative:** Starts with small refactorings, builds confidence
- **Framework-Aware:** Explicitly mentions framework version in requests

---

## Success Criteria for RefactorCsharpMCP

### Must-Have Capabilities
1. **Framework Version Awareness:** CRITICAL - must respect .NET Framework 4.x vs .NET 8 differences
2. **Extract Method:** Breaking down long methods (daily use)
3. **Extract Class:** Refactoring god classes (weekly use)
4. **Safe Delete:** Verifying code is unused before deleting (weekly use)
5. **Constructor Injection:** Introducing testability (weekly use)
6. **Make Field Readonly:** Improving immutability (occasional use)

### Nice-to-Have Capabilities
1. **Inline Method:** Removing over-abstraction
2. **Rename Symbol:** Improving clarity
3. **Remove Unused Usings:** Cleanup

### Deal-Breakers
1. ❌ **Framework Incompatibility:** C# 12 features in .NET Framework 4.8 code
2. ❌ **Semantic Changes:** Any behavioral change after refactoring
3. ❌ **Cross-File Breaking:** Refactoring that breaks other files/assemblies
4. ❌ **Unclear Limitations:** Must clearly state what tool CAN'T do
5. ❌ **Compilation Errors:** Refactored code must compile immediately

### Risk Tolerance
- **Zero tolerance** for production-breaking changes
- **Willing to validate** generated code extensively
- **Prefers conservative refactorings** over aggressive transformations
- **Values predictability** over comprehensiveness

---

## Quotes

> "I've seen too many automated refactorings break production. I need guarantees."

> "The biggest fear is introducing C# 8 features into Framework 4.8 code. That's a silent compiler error waiting to happen."

> "If the tool says 'Safe Delete', I still search the entire solution. Can't be too careful with legacy code."

> "I'd rather have a tool that does 5 things perfectly than 50 things poorly."

> "Claude Code is great, but I verify everything. Trust is earned, not given."

> "My job is to keep legacy systems running while slowly modernizing. Breaking changes are career-limiting."

> "Framework awareness isn't a feature, it's a requirement. Without it, the tool is useless for legacy codebases."

---

## Demographics & Context

### Team Environment
- **Team Size:** 12 developers (4 senior, 6 mid, 2 junior)
- **Code Review:** All PRs require 2 approvals (senior developers)
- **Standards:** Strict coding standards, mandatory code reviews
- **Architecture:** Layered monolith (Data Access → Business Logic → UI)
- **Culture:** Risk-averse, change-resistant, quality-focused

### Project Characteristics
- **Legacy Codebase:** 1.2M LOC across 150+ projects
- **Frameworks:** Mixed (.NET Framework 4.6.2, 4.7.2, 4.8, pilot .NET 8)
- **Age:** 15+ years, some code from 2009
- **Test Coverage:** 30% (improving slowly)
- **Deployment:** Manual deployments, bi-weekly releases
- **Criticality:** High - revenue-generating ERP system

### Learning Style
- **Prefers:** Detailed documentation, step-by-step guides
- **Documentation:** Reads thoroughly before trying new tools
- **Troubleshooting:** Methodical debugging, root cause analysis
- **Risk Assessment:** Evaluates worst-case scenarios before adopting tools

---

## Relationship with AI-Assisted Development

### Claude Code Usage Patterns
- **Frequency:** 5-10 interactions per day (selective use)
- **Primary Use Cases:**
  - Code explanation (40%) - understanding legacy code
  - Refactoring (30%) - guided by AI, validated by Mike
  - Documentation (15%) - generating XML comments
  - Test generation (10%) - creating unit tests
  - Research (5%) - learning .NET 8 features
- **Trust Level:** Medium - verifies all outputs
- **Workflow Integration:** Supplemental (not core workflow yet)

### Skepticism & Concerns
1. **Accuracy:** "Will the AI understand complex legacy code?"
2. **Framework Compatibility:** "Will it introduce breaking changes?"
3. **Hidden Bugs:** "What if the refactoring subtly changes behavior?"
4. **Team Adoption:** "Will my team trust AI-generated code?"
5. **Vendor Lock-In:** "What if Claude Code changes/discontinues?"

### Path to Trust
Mike builds trust through:
1. **Small Experiments:** Start with low-risk refactorings
2. **Validation:** Extensive testing and code review
3. **Incremental Adoption:** Use on non-critical code first
4. **Team Demonstration:** Show successful refactorings to team
5. **Documentation:** Build team knowledge base of safe patterns

---

## Migration Journey (.NET Framework → .NET 8)

### Current State
- **12 legacy services** on .NET Framework 4.6.2 - 4.8
- **2 pilot services** migrated to .NET 8
- **Timeline:** 3-year migration plan (conservative)

### Challenges in Migration
1. **Breaking API Changes:** Framework-specific APIs don't exist in .NET 8
2. **Language Features:** Can't use C# 8-12 features in Framework code
3. **Testing Gaps:** Legacy code lacks tests, making migration risky
4. **Team Knowledge:** Team comfortable with Framework, learning .NET 8

### How RefactorCsharpMCP Helps
1. **Framework-Specific Refactoring:**
   - Use `targetFramework="net48"` for legacy code (C# 7.3 syntax)
   - Use `targetFramework="net8.0"` for migrated services (C# 12 syntax)
2. **Incremental Modernization:**
   - Refactor legacy code to be more modular before migration
   - Extract testable components
   - Introduce DI patterns compatible with both frameworks
3. **Learning Tool:**
   - See modern C# patterns in .NET 8 refactorings
   - Compare Framework vs .NET 8 idioms
   - Accelerate team's .NET 8 learning curve

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-10-09 | Initial persona based on PRD v1.1.0 |

---

**Persona Owner:** Product Owner (Master)
**Last Review:** 2025-10-09
**Next Review:** After V1 release user feedback
