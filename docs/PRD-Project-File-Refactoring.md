# Product Requirements Document: .NET Project File Refactoring for RefactorCsharpMCP

**Version:** 1.0.0
**Date:** 2025-11-02
**Status:** Draft - Ready for Review
**Author:** Principal Product Owner
**Supporting Research:** [project-file-refactoring-analysis.md](project-file-refactoring-analysis.md)

---

## Executive Summary

### The Problem

.NET developers spend **hours managing project files manually** across large solutions, leading to:
- **Version conflicts** that cause runtime bugs (different NuGet package versions across 15 projects)
- **Blocked modernization** due to complex SDK-style migration (200-line legacy .csproj files)
- **Risky framework upgrades** with no automated API compatibility checking (.NET Framework 4.8 → .NET 8)
- **Technical debt accumulation** from inability to adopt modern best practices (Central Package Management)

**Quantified Impact:**
- Manual package synchronization: **2 hours per solution** for 10+ projects
- SDK-style migration: **4 hours per project** with 85% automation (15% manual cleanup)
- Framework upgrade analysis: **8+ hours per project** with high risk of runtime errors

### The Solution

RefactorCsharpMCP will provide **AI-assisted .NET project file refactoring** via MCP tools, enabling:
- **One-click package synchronization** across entire solutions (2 hours → 5 minutes)
- **Automated SDK-style migration** with validation and rollback (4 hours → 30 minutes)
- **AI-guided framework upgrades** with compatibility analysis (8 hours → 2 hours)
- **Instant CPM adoption** with conflict resolution and version alignment

### Scope

**V1.0 MVP (3 refactorings):**
1. **Package Reference Management** (P0) - Add/update/remove NuGet packages with bulk operations
2. **SDK-Style Project Conversion** (P0) - Migrate legacy .csproj to modern format
3. **Central Package Management Migration** (P1) - Enable CPM across solutions

**V1.5 Enhancements (3 additional refactorings):**
4. **Update Target Framework** (P1) - Framework migration with API compatibility analysis
5. **Project Reference Management** (P2) - Add/remove project references with path handling
6. **Property Group Synchronization** (P2) - Ensure consistent build properties

### Success Criteria

**Adoption:**
- 100 active users by Month 3, 500 by Month 6 (MCP tool invocations)

**Value Delivery:**
- Package Management: 23x time savings (2 hours → 5 minutes)
- SDK Migration: 8x time savings (4 hours → 30 minutes)

**Quality:**
- ≥95% refactoring success rate
- ≥98% post-refactoring build success rate

**User Satisfaction:**
- Net Promoter Score (NPS) ≥30 by Month 6

---

## Problem Statement

### Current State Pain Points

#### Pain Point 1: Package Version Chaos (Severity: Critical)

**Problem:**
Large .NET solutions accumulate **inconsistent NuGet package versions** across projects, causing:
- Runtime bugs from assembly binding failures
- Debugging time wasted tracking version mismatches
- Security vulnerabilities from outdated packages in some projects

**User Impact:**
- **Mike (Legacy Maintainer):** Spends 2+ hours per sprint manually synchronizing package versions across 15-project ERP solution
- **AI Agent:** Cannot reliably suggest package updates without cross-project version awareness

**Quantified Cost:**
- 2 hours × 26 sprints/year = **52 hours/year per developer** on manual package management

**Current Workarounds:**
- ❌ Manual editing of each .csproj file
- ❌ NuGet Package Manager UI (one project at a time)
- ❌ Text search-and-replace (error-prone)

---

#### Pain Point 2: Modernization Blocked by Legacy Project Format (Severity: High)

**Problem:**
Legacy .csproj files (pre-VS2017) have:
- **200+ lines** of boilerplate (explicit file listings)
- No support for modern features (Directory.Build.props, Central Package Management)
- Difficult merge conflicts in version control

**User Impact:**
- **Mike:** Cannot adopt CPM without first migrating to SDK-style (double work)
- **Sarah:** Onboarding friction when switching between modern (.NET 8) and legacy (.NET Framework 4.8) projects

**Quantified Cost:**
- 4 hours × 20 projects = **80 hours per solution** for manual migration
- Community tools achieve only 85% automation (leaves 15% manual work)

**Current Workarounds:**
- ❌ Manual editing (time-consuming, error-prone)
- ❌ CsprojToVs2017 tool (requires manual cleanup)
- ❌ Stay on legacy format (blocks modernization)

---

#### Pain Point 3: Risky Framework Upgrades (Severity: High)

**Problem:**
Migrating .NET Framework projects to modern .NET requires:
- Manual API compatibility analysis (no tooling)
- Runtime-only errors not caught at compile time
- Package compatibility validation for new framework

**User Impact:**
- **Mike:** Blocked from migrating 15-year-old ERP system to .NET 8 (too risky without automated analysis)
- **Sarah:** Framework upgrade pilot projects delayed by compatibility unknowns

**Quantified Cost:**
- 8+ hours per project for manual analysis
- High risk of production bugs from missed incompatibilities

**Current Workarounds:**
- ❌ Manual code review (incomplete)
- ❌ .NET Upgrade Assistant (limited scope, no MCP integration)
- ❌ Stay on .NET Framework (accumulate technical debt)

---

#### Pain Point 4: CPM Adoption Friction (Severity: Medium)

**Problem:**
Central Package Management is now industry best practice (NuGet 6.2+, .NET SDK 6.0+) but adoption is hindered by:
- Manual creation of Directory.Packages.props
- Extracting versions from all .csproj files
- Resolving version conflicts across projects

**User Impact:**
- **Sarah:** Wants CPM for new microservices but setup takes 1+ hours per solution
- **Mike:** Cannot justify CPM adoption time for legacy solutions

**Quantified Cost:**
- 1-2 hours per solution for manual setup
- Missed benefits: simplified dependency management, easier auditing, conflict prevention

**Current Workarounds:**
- ❌ Manual setup (time-consuming)
- ❌ Skip CPM adoption (miss best practice benefits)

---

## Goals and Non-Goals

### Primary Goals

1. **Eliminate Manual Package Management Toil**
   - One-click package updates across entire solution
   - Automatic version conflict detection and resolution
   - Framework compatibility validation

2. **Accelerate .NET Modernization**
   - Automated SDK-style migration with validation
   - AI-guided framework upgrades with compatibility analysis
   - Enable CPM adoption with one command

3. **Maintain Build Reliability**
   - Post-refactoring validation (`dotnet build`)
   - Automatic backup and rollback on failure
   - Dry-run mode for preview before applying

4. **Seamless AI Integration**
   - MCP tools for conversational refactoring
   - Multi-project batch operations
   - Clear error messages for AI agent handling

### Success Criteria

**Functional:**
- ✅ All 3 MVP refactorings implemented and tested
- ✅ Batch operations work across 10+ project solutions
- ✅ Dry-run mode provides accurate preview
- ✅ Rollback restores to pre-refactoring state

**Quality:**
- ✅ ≥95% refactoring success rate (no errors)
- ✅ ≥98% build validation rate (refactored projects build successfully)
- ✅ ≥90% test coverage for refactoring logic

**Performance:**
- ✅ Package management: <10 seconds for 10-project solution
- ✅ SDK migration: <30 seconds per project
- ✅ CPM migration: <20 seconds for 10-project solution

**User Experience:**
- ✅ Net Promoter Score (NPS) ≥30 by Month 6
- ✅ 100 active users by Month 3, 500 by Month 6
- ✅ ≥80% of errors resolved within 10 minutes (clear error messages)

### Non-Goals (Explicitly Out of Scope)

**V1.0 MVP Non-Goals:**

❌ **CI/CD Integration**
- No pipeline configuration management
- No NuGet feed setup or private package source management
- No automated dependency updates on schedule
- **Rationale:** RefactorCsharpMCP is a development-time tool, not a build-time tool

❌ **Type Renaming / File Moving**
- No cross-file type renaming (requires workspace analysis)
- No file path updates when moving projects
- No namespace refactoring
- **Rationale:** Scope limited to project file XML manipulation, not code refactoring

❌ **Multi-Framework Refactoring**
- No cross-framework compatibility analysis
- No multi-targeting project support in V1
- **Rationale:** Defer to V1.5 after single-framework refactorings validated

❌ **Custom MSBuild Task Manipulation**
- No custom target modification
- No build event editing
- No MSBuild condition editing
- **Rationale:** High complexity, low user demand

❌ **Package Source Management**
- No NuGet.config modification
- No private feed configuration
- No package source authentication
- **Rationale:** Existing tooling sufficient (`dotnet nuget` CLI)

❌ **Solution File Manipulation (V1.0)**
- No solution folder reorganization
- No project addition/removal from solution
- **Rationale:** Defer to V1.5 (lower priority than core refactorings)

---

## User Personas and Use Cases

### Persona 1: Mike - Legacy Code Maintainer

**Background:**
- Maintains 15-year-old .NET Framework 4.6.2-4.8.1 ERP system
- 20 projects in solution, legacy .csproj format
- Different NuGet package versions across projects (accumulated over years)
- **Top Priority:** Stability - cannot risk production outages

**Use Case 1: Emergency Package Security Patch**

**Scenario:** Newtonsoft.Json security vulnerability (CVE-2024-XXXX) requires update to 13.0.3 across all 20 projects.

**Current Workflow (2 hours):**
1. Open each .csproj in text editor
2. Find `<PackageReference Include="Newtonsoft.Json" Version="..." />`
3. Update version to 13.0.3
4. Save file
5. Repeat for 20 projects
6. Build solution to verify no breaks
7. Manual testing to verify runtime compatibility

**Desired Workflow with RefactorCsharpMCP (5 minutes):**
1. **AI Agent:** "Update Newtonsoft.Json to 13.0.3 across entire solution"
2. **Tool:** `project_manage_package_reference` with `applyToAllProjects: true`
3. **Preview:** Shows all 20 projects with version changes
4. **Mike:** Reviews preview, approves
5. **Tool:** Updates all projects, validates build, reports success
6. **Mike:** Commits changes with confidence

**Value Delivered:** 23x time savings (2 hours → 5 minutes)

---

**Use Case 2: Modernization Preparation**

**Scenario:** Management approved .NET 8 migration pilot. Mike needs to prepare 5 critical projects by migrating to SDK-style format first (prerequisite for framework upgrade).

**Current Workflow (20 hours):**
1. Run CsprojToVs2017 tool on each project
2. Manual cleanup of generated files (tool achieves 85% automation)
3. Verify no files excluded from build (implicit includes)
4. Fix broken references
5. Test build
6. Repeat for 5 projects

**Desired Workflow with RefactorCsharpMCP (2.5 hours):**
1. **Mike:** "Migrate these 5 projects to SDK-style format"
2. **Tool:** `project_convert_to_sdk_style` with dry-run preview
3. **Preview:** Shows before/after .csproj diff for each project
4. **Mike:** Reviews, identifies potential issues (ASP.NET Web Apps)
5. **Tool:** Executes migration with backup, validates build
6. **Mike:** Manual verification of edge cases (30 minutes per project)

**Value Delivered:** 8x time savings (4 hours → 30 minutes per project)

---

### Persona 2: Sarah - Full-Stack Developer

**Background:**
- Works on modern .NET 8 microservices
- Creates new projects frequently (2-3 per month)
- Uses Claude Code for AI-assisted development
- **Top Priority:** Velocity - minimize setup time, maximize coding time

**Use Case 3: New Microservice Setup with CPM**

**Scenario:** Sarah creates 3 new microservices (API Gateway, Order Service, Inventory Service) and wants consistent package management from day one.

**Current Workflow (90 minutes):**
1. Create 3 projects with `dotnet new webapi`
2. Manually create Directory.Build.props
3. Manually create Directory.Packages.props
4. Add common packages to Directory.Packages.props (logging, config, health checks)
5. Remove Version attributes from each project's PackageReferences
6. Test build to verify CPM working

**Desired Workflow with RefactorCsharpMCP (10 minutes):**
1. **Sarah:** Creates 3 projects with `dotnet new webapi`
2. **Claude Code:** "Enable Central Package Management for this solution"
3. **Tool:** `project_enable_central_package_management` analyzes 3 projects
4. **Tool:** Creates Directory.Build.props, Directory.Packages.props
5. **Tool:** Updates all 3 projects, validates build
6. **Sarah:** Coding begins immediately

**Value Delivered:** 9x time savings (90 minutes → 10 minutes)

---

**Use Case 4: Adding Shared Package Across Services**

**Scenario:** Sarah needs to add OpenTelemetry tracing to all 8 microservices in her solution.

**Current Workflow (45 minutes):**
1. Open NuGet Package Manager in VS
2. Select each project individually
3. Search for OpenTelemetry.Exporter.Console
4. Install version 1.7.0
5. Repeat for 8 projects
6. Manually verify all projects have same version

**Desired Workflow with RefactorCsharpMCP (2 minutes):**
1. **Sarah:** "Add OpenTelemetry.Exporter.Console 1.7.0 to all projects"
2. **Tool:** `project_manage_package_reference` with `applyToAllProjects: true`
3. **Tool:** Adds to Directory.Packages.props (CPM enabled)
4. **Tool:** Updates all 8 projects, validates build
5. **Sarah:** Continues coding

**Value Delivered:** 22x time savings (45 minutes → 2 minutes)

---

### Persona 3: AI Coding Agent (Claude Code)

**Background:**
- Assists developers with .NET refactoring tasks
- Has access to project files (.csproj, .sln)
- Can invoke MCP tools autonomously
- **Top Priority:** Accuracy - provide correct refactoring suggestions without breaking builds

**Use Case 5: Proactive Package Update Suggestion**

**Scenario:** AI Agent detects package version inconsistency during code review.

**Workflow:**
1. **Developer:** "Review my pull request"
2. **Agent:** Reads .csproj files, detects inconsistency:
   - ProjectA uses Newtonsoft.Json 12.0.3
   - ProjectB uses Newtonsoft.Json 13.0.1
   - ProjectC uses Newtonsoft.Json 12.0.3
3. **Agent:** "I noticed package version inconsistencies. Shall I synchronize to 13.0.1?"
4. **Developer:** "Yes, do it"
5. **Agent:** Invokes `project_manage_package_reference` with:
   ```json
   {
     "operation": "update",
     "packageId": "Newtonsoft.Json",
     "version": "13.0.1",
     "applyToAllProjects": true,
     "dryRun": true
   }
   ```
6. **Agent:** Reviews preview, confirms no breaking changes
7. **Agent:** Re-invokes with `dryRun: false`
8. **Agent:** "Updated 3 projects to Newtonsoft.Json 13.0.1. Build validated successfully."

**Value Delivered:** Proactive issue detection prevents future runtime bugs

---

## Product Requirements

### V1.0 MVP (3 Refactorings)

---

### Requirement 1: Package Reference Management (P0)

**Description:**
Add, update, or remove NuGet package references across single or multiple projects with framework compatibility validation and version conflict detection.

**User Story:**
> As a .NET developer, I want to update NuGet packages across my entire solution with one command, so that I can maintain consistent versions without manual editing.

**Acceptance Criteria:**

**AC-1.1: Single Project Package Addition**
- Given a valid .csproj file path
- When I invoke `project_manage_package_reference` with `operation: "add"`
- Then the package is added to the project with specified version
- And no duplicate PackageReference entries exist
- And the project builds successfully (`dotnet build`)

**AC-1.2: Multi-Project Package Update**
- Given a solution directory with 10+ projects
- When I invoke with `applyToAllProjects: true` and `operation: "update"`
- Then all projects using the package are updated to the new version
- And I receive a summary: `{ filesModified: 8, filesSkipped: 2, reason: "package not present" }`
- And all modified projects build successfully

**AC-1.3: Framework Compatibility Validation**
- Given a package that requires .NET 6.0+
- When I attempt to add it to a .NET Framework 4.8 project
- Then I receive error: `"Package System.Text.Json 8.0.0 requires net6.0+, but project targets net48"`
- And suggested alternative: `"Use Newtonsoft.Json 13.0.3 for net48 compatibility"`

**AC-1.4: Version Conflict Detection**
- Given 3 projects with different package versions (10.0.3, 12.0.3, 13.0.1)
- When I invoke `project_enable_central_package_management`
- Then I receive conflict report:
  ```json
  {
    "conflicts": [
      {
        "packageId": "Newtonsoft.Json",
        "versions": ["10.0.3", "12.0.3", "13.0.1"],
        "projects": ["ProjectA.csproj", "ProjectB.csproj", "ProjectC.csproj"],
        "suggestedVersion": "13.0.1",
        "strategy": "highest"
      }
    ]
  }
  ```
- And I can choose resolution strategy: `"highest"`, `"manual"`, or `"fail"`

**AC-1.5: Dry-Run Preview**
- Given `dryRun: true` parameter
- When I invoke any package operation
- Then I receive preview without modifying files:
  ```json
  {
    "preview": true,
    "changes": [
      { "file": "ProjectA.csproj", "action": "add", "package": "Newtonsoft.Json", "version": "13.0.3" },
      { "file": "ProjectB.csproj", "action": "update", "oldVersion": "12.0.3", "newVersion": "13.0.3" }
    ],
    "estimatedTime": "5 seconds"
  }
  ```

**AC-1.6: Automatic Backup and Rollback**
- Given a package update that fails build validation
- When the tool detects `dotnet build` errors
- Then it automatically restores from `.csproj.backup` files
- And returns error with build diagnostics:
  ```json
  {
    "success": false,
    "errorCode": "BUILD_VALIDATION_FAILED",
    "message": "Build failed after package update. Changes rolled back.",
    "buildErrors": ["CS0246: The type or namespace 'OldClass' could not be found"],
    "rollbackPerformed": true
  }
  ```

**MCP Tool Signature:**

```json
{
  "name": "project_manage_package_reference",
  "description": "Add, update, or remove NuGet package references with framework compatibility validation and batch operations",
  "inputSchema": {
    "type": "object",
    "properties": {
      "projectPath": {
        "type": "string",
        "description": "Absolute path to .csproj file OR solution directory (for applyToAllProjects)"
      },
      "operation": {
        "type": "string",
        "enum": ["add", "update", "remove"],
        "description": "Operation to perform on package reference"
      },
      "packageId": {
        "type": "string",
        "description": "NuGet package identifier (e.g., 'Newtonsoft.Json')"
      },
      "version": {
        "type": "string",
        "description": "Package version for add/update operations (e.g., '13.0.3')"
      },
      "applyToAllProjects": {
        "type": "boolean",
        "default": false,
        "description": "Apply operation to all projects in solution (requires projectPath to be solution directory)"
      },
      "targetFramework": {
        "type": "string",
        "description": "Target framework for compatibility validation (e.g., 'net8.0', 'net48'). Optional - inferred from project if not specified."
      },
      "dryRun": {
        "type": "boolean",
        "default": false,
        "description": "Preview changes without modifying files"
      },
      "validateBuild": {
        "type": "boolean",
        "default": true,
        "description": "Validate project builds after operation. Auto-rollback on failure."
      }
    },
    "required": ["projectPath", "operation", "packageId"]
  }
}
```

**Success Metrics:**
- **Time Savings:** 23x improvement (2 hours → 5 minutes for 10-project solution)
- **Success Rate:** ≥95% operations complete without errors
- **Build Validation:** ≥98% of modified projects build successfully
- **Adoption:** Most-used project refactoring tool (hypothesis: 50% of total invocations)

---

### Requirement 2: SDK-Style Project Conversion (P0)

**Description:**
Migrate legacy .csproj files (Visual Studio 2015 and earlier) to modern SDK-style format with automatic cleanup, validation, and rollback.

**User Story:**
> As a .NET maintainer, I want to convert legacy .csproj files to SDK-style format automatically, so that I can adopt modern tooling (CPM, Directory.Build.props) without 4 hours of manual editing per project.

**Acceptance Criteria:**

**AC-2.1: Basic SDK-Style Conversion**
- Given a legacy .csproj file (200+ lines with explicit file listings)
- When I invoke `project_convert_to_sdk_style`
- Then the project is converted to SDK-style format:
  - Project element has `Sdk="Microsoft.NET.Sdk"` attribute
  - Explicit file listings removed (implicit include patterns)
  - Essential properties preserved (OutputType, AssemblyName, RootNamespace, TargetFramework)
  - PackageReferences preserved or migrated from packages.config
  - ProjectReferences preserved with relative paths
- And resulting file is 10-20 lines (vs. original 200+)
- And project builds successfully

**AC-2.2: Framework Version Mapping**
- Given legacy project with `<TargetFrameworkVersion>v4.5.2</TargetFrameworkVersion>`
- When converted to SDK-style
- Then TargetFramework is mapped to TFM: `<TargetFramework>net452</TargetFramework>`
- And C# language version is set correctly for framework (net452 → C# 6.0)

**AC-2.3: ASP.NET Web App Detection**
- Given a legacy ASP.NET Web App project (contains `<UseIISExpress>`)
- When I invoke conversion
- Then I receive warning:
  ```json
  {
    "success": false,
    "errorCode": "WEB_APP_REQUIRES_MANUAL_REVIEW",
    "message": "ASP.NET Web Apps require specialized SDK (MSBuild.SDK.SystemWeb). Please review docs/sdk-migration-web-apps.md before proceeding.",
    "projectType": "ASP.NET Web Application",
    "recommendedSDK": "MSBuild.SDK.SystemWeb"
  }
  ```
- And conversion does NOT proceed automatically (requires user confirmation)

**AC-2.4: Implicit Include Validation**
- Given a legacy project with EmbeddedResource items with custom settings
- When converted to SDK-style
- Then these items are preserved explicitly:
  ```xml
  <ItemGroup>
    <EmbeddedResource Include="Resources\Icon.png" />
    <None Include="config.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
  ```
- And standard .cs files use implicit includes
- And bin/, obj/ folders are implicitly excluded

**AC-2.5: Dry-Run with Before/After Diff**
- Given `dryRun: true` parameter
- When I invoke conversion
- Then I receive side-by-side diff:
  ```json
  {
    "preview": true,
    "before": "<Project ToolsVersion=\"15.0\"...>",
    "after": "<Project Sdk=\"Microsoft.NET.Sdk\">...",
    "lineCountReduction": "215 lines → 18 lines",
    "preservedElements": ["OutputType", "AssemblyName", "TargetFramework", "3 PackageReferences", "2 ProjectReferences"],
    "warnings": ["EmbeddedResource with custom settings preserved explicitly"]
  }
  ```

**AC-2.6: Backup and Rollback**
- Given conversion fails build validation
- When tool detects `dotnet build` errors
- Then original .csproj is restored from `.csproj.backup`
- And I receive error with rollback confirmation:
  ```json
  {
    "success": false,
    "errorCode": "BUILD_FAILED_AFTER_CONVERSION",
    "message": "Conversion completed but project failed to build. Changes rolled back.",
    "buildErrors": ["CS0234: The namespace 'System.Web' does not exist"],
    "rollbackPerformed": true,
    "backup": "MyProject.csproj.backup"
  }
  ```

**MCP Tool Signature:**

```json
{
  "name": "project_convert_to_sdk_style",
  "description": "Convert legacy .csproj files to modern SDK-style format with validation and rollback. Handles packages.config migration, implicit includes, and framework mapping.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "projectPath": {
        "type": "string",
        "description": "Absolute path to legacy .csproj file"
      },
      "dryRun": {
        "type": "boolean",
        "default": false,
        "description": "Preview conversion without modifying files (shows before/after diff)"
      },
      "validateBuild": {
        "type": "boolean",
        "default": true,
        "description": "Validate project builds after conversion. Auto-rollback on failure."
      },
      "allowWebApps": {
        "type": "boolean",
        "default": false,
        "description": "Allow conversion of ASP.NET Web Apps (requires manual review). Default: false (safer)."
      }
    },
    "required": ["projectPath"]
  }
}
```

**Success Metrics:**
- **Time Savings:** 8x improvement (4 hours → 30 minutes per project)
- **Automation Rate:** ≥90% conversion success without manual intervention (better than community tool's 85%)
- **Build Validation:** ≥95% of converted projects build successfully
- **Line Reduction:** Average 10x reduction in .csproj file size (200 lines → 20 lines)

---

### Requirement 3: Central Package Management Migration (P1)

**Description:**
Migrate solutions to Central Package Management (CPM) by creating Directory.Build.props and Directory.Packages.props, extracting versions, and resolving conflicts.

**User Story:**
> As a .NET architect, I want to enable Central Package Management across my solution with one command, so that I can enforce consistent package versions without 2 hours of manual setup.

**Acceptance Criteria:**

**AC-3.1: CPM Scaffolding**
- Given a solution directory with 8 projects (no existing CPM)
- When I invoke `project_enable_central_package_management`
- Then the following files are created:
  - `Directory.Build.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
  - `Directory.Packages.props` with all package versions extracted from projects
- And all .csproj files are updated (Version attributes removed from PackageReferences)
- And solution builds successfully

**AC-3.2: Version Conflict Resolution**
- Given 3 projects with conflicting package versions:
  - ProjectA: Newtonsoft.Json 10.0.3
  - ProjectB: Newtonsoft.Json 12.0.3
  - ProjectC: Newtonsoft.Json 13.0.1
- When I invoke with default `conflictResolutionStrategy: "fail"`
- Then I receive error with conflict details:
  ```json
  {
    "success": false,
    "errorCode": "PACKAGE_VERSION_CONFLICTS",
    "conflicts": [
      {
        "packageId": "Newtonsoft.Json",
        "versions": ["10.0.3", "12.0.3", "13.0.1"],
        "projects": ["ProjectA", "ProjectB", "ProjectC"]
      }
    ],
    "message": "Version conflicts detected. Specify conflictResolutionStrategy: 'highest' or 'manual' to proceed."
  }
  ```

**AC-3.3: Automatic Conflict Resolution (Opt-In)**
- Given conflicts exist
- When I invoke with `conflictResolutionStrategy: "highest"`
- Then all projects are updated to highest version (13.0.1)
- And I receive summary:
  ```json
  {
    "success": true,
    "message": "CPM enabled with automatic conflict resolution (highest version strategy)",
    "conflictsResolved": [
      {
        "packageId": "Newtonsoft.Json",
        "resolvedVersion": "13.0.1",
        "upgradedProjects": ["ProjectA", "ProjectB"]
      }
    ],
    "filesCreated": ["Directory.Build.props", "Directory.Packages.props"],
    "filesModified": ["ProjectA.csproj", "ProjectB.csproj", "ProjectC.csproj"]
  }
  ```

**AC-3.4: Framework-Specific Version Handling**
- Given multi-framework projects:
  - ProjectA targets net8.0 (uses System.Text.Json 8.0.0)
  - ProjectB targets net48 (uses Newtonsoft.Json 13.0.3, cannot use System.Text.Json)
- When CPM is enabled
- Then Directory.Packages.props uses conditional version management:
  ```xml
  <PackageVersion Include="System.Text.Json" Version="8.0.0" Condition="'$(TargetFramework)' == 'net8.0'" />
  <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
  ```

**AC-3.5: Dry-Run Preview**
- Given `dryRun: true`
- When I invoke CPM migration
- Then I receive preview:
  ```json
  {
    "preview": true,
    "filesToCreate": [
      {
        "path": "Directory.Build.props",
        "content": "<Project>...</Project>"
      },
      {
        "path": "Directory.Packages.props",
        "packages": [
          { "id": "Newtonsoft.Json", "version": "13.0.1" },
          { "id": "Serilog", "version": "3.1.1" }
        ]
      }
    ],
    "filesToModify": [
      { "path": "ProjectA.csproj", "changes": "Remove Version attributes from 3 PackageReferences" }
    ],
    "conflicts": [],
    "estimatedTime": "15 seconds"
  }
  ```

**MCP Tool Signature:**

```json
{
  "name": "project_enable_central_package_management",
  "description": "Enable Central Package Management (CPM) across solution with automatic version extraction, conflict resolution, and validation. Requires .NET SDK 6.0+.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "solutionPath": {
        "type": "string",
        "description": "Absolute path to solution directory (contains .sln file)"
      },
      "conflictResolutionStrategy": {
        "type": "string",
        "enum": ["fail", "highest", "manual"],
        "default": "fail",
        "description": "How to handle version conflicts: 'fail' (return error), 'highest' (auto-select highest version), 'manual' (prompt user)"
      },
      "dryRun": {
        "type": "boolean",
        "default": false,
        "description": "Preview changes without modifying files"
      },
      "validateBuild": {
        "type": "boolean",
        "default": true,
        "description": "Validate solution builds after CPM enablement. Auto-rollback on failure."
      }
    },
    "required": ["solutionPath"]
  }
}
```

**Success Metrics:**
- **Time Savings:** 9x improvement (90 minutes → 10 minutes per solution)
- **Success Rate:** ≥95% migrations complete without manual intervention
- **Conflict Detection:** 100% of version conflicts identified and reported
- **Build Validation:** ≥98% of migrated solutions build successfully

---

## User Experience

### MCP Tool Interaction Patterns

**Pattern 1: Single-Project Refactoring**

```
User: "Update Serilog to 3.1.1 in MyService.csproj"

AI Agent → Tool:
{
  "tool": "project_manage_package_reference",
  "projectPath": "/path/to/MyService.csproj",
  "operation": "update",
  "packageId": "Serilog",
  "version": "3.1.1",
  "dryRun": false,
  "validateBuild": true
}

Tool → AI Agent:
{
  "success": true,
  "message": "Updated Serilog to 3.1.1 in MyService.csproj",
  "filesModified": ["/path/to/MyService.csproj"],
  "buildValidation": "PASSED",
  "executionTime": "2.3s"
}

AI Agent → User:
"✓ Updated Serilog to 3.1.1. Build validated successfully."
```

---

**Pattern 2: Multi-Project Batch Operation with Conflicts**

```
User: "Synchronize Newtonsoft.Json across all projects"

AI Agent → Tool (Step 1 - Discovery):
{
  "tool": "project_manage_package_reference",
  "projectPath": "/path/to/solution",
  "operation": "update",
  "packageId": "Newtonsoft.Json",
  "version": "latest",
  "applyToAllProjects": true,
  "dryRun": true
}

Tool → AI Agent:
{
  "preview": true,
  "conflicts": [
    {
      "packageId": "Newtonsoft.Json",
      "versions": ["10.0.3", "12.0.3", "13.0.1"],
      "projects": ["ProjectA", "ProjectB", "ProjectC"]
    }
  ],
  "changes": [
    { "file": "ProjectA.csproj", "action": "update", "oldVersion": "10.0.3", "newVersion": "13.0.1" }
  ]
}

AI Agent → User:
"I found version conflicts:
- ProjectA: 10.0.3
- ProjectB: 12.0.3
- ProjectC: 13.0.1

I recommend updating all to 13.0.1 (latest). Proceed?"

User: "Yes"

AI Agent → Tool (Step 2 - Execute):
{
  "tool": "project_manage_package_reference",
  "projectPath": "/path/to/solution",
  "operation": "update",
  "packageId": "Newtonsoft.Json",
  "version": "13.0.1",
  "applyToAllProjects": true,
  "dryRun": false
}

Tool → AI Agent:
{
  "success": true,
  "message": "Updated Newtonsoft.Json to 13.0.1 across 3 projects",
  "filesModified": ["ProjectA.csproj", "ProjectB.csproj"],
  "filesSkipped": ["ProjectC.csproj (already 13.0.1)"],
  "buildValidation": "PASSED (3/3 projects)",
  "executionTime": "8.7s"
}

AI Agent → User:
"✓ Synchronized Newtonsoft.Json to 13.0.1 across solution:
- ProjectA: 10.0.3 → 13.0.1 ✓
- ProjectB: 12.0.3 → 13.0.1 ✓
- ProjectC: 13.0.1 (unchanged)

All builds validated successfully."
```

---

**Pattern 3: SDK Migration with Warnings**

```
User: "Migrate OrderService.csproj to SDK-style"

AI Agent → Tool:
{
  "tool": "project_convert_to_sdk_style",
  "projectPath": "/path/to/OrderService.csproj",
  "dryRun": true
}

Tool → AI Agent:
{
  "preview": true,
  "warnings": [
    "ASP.NET Web Application detected. Requires MSBuild.SDK.SystemWeb (not Microsoft.NET.Sdk)."
  ],
  "before": "<Project ToolsVersion=\"15.0\"...> (234 lines)",
  "after": "<Project Sdk=\"MSBuild.SDK.SystemWeb\">... (22 lines)",
  "lineCountReduction": "234 → 22 lines (90% reduction)",
  "preservedElements": ["OutputType", "AssemblyName", "TargetFramework", "8 PackageReferences"]
}

AI Agent → User:
"⚠️ OrderService is an ASP.NET Web Application. This requires specialized SDK (MSBuild.SDK.SystemWeb).

Preview shows 90% reduction (234 → 22 lines). Preserved: OutputType, AssemblyName, 8 PackageReferences.

This is riskier than standard SDK migration. Review docs/sdk-migration-web-apps.md first. Proceed with conversion?"

User: "Yes, I've reviewed the docs"

AI Agent → Tool:
{
  "tool": "project_convert_to_sdk_style",
  "projectPath": "/path/to/OrderService.csproj",
  "dryRun": false,
  "allowWebApps": true,
  "validateBuild": true
}

Tool → AI Agent:
{
  "success": true,
  "message": "Converted OrderService.csproj to SDK-style (MSBuild.SDK.SystemWeb)",
  "filesModified": ["OrderService.csproj"],
  "backup": "OrderService.csproj.backup",
  "buildValidation": "PASSED",
  "warnings": [
    "Manual verification recommended for Web.config transformations",
    "Test IIS Express compatibility before deployment"
  ]
}

AI Agent → User:
"✓ Converted OrderService.csproj to SDK-style (234 → 22 lines)
Backup saved: OrderService.csproj.backup
Build validated successfully.

⚠️ Important: Manually verify Web.config transformations and IIS Express compatibility."
```

---

### Error Handling and Recovery

**Error Scenario 1: Framework Incompatibility**

```json
{
  "success": false,
  "errorCode": "FRAMEWORK_INCOMPATIBILITY",
  "category": "ValidationError",
  "message": "Package System.Text.Json 8.0.0 requires net6.0+, but project targets net48",
  "packageId": "System.Text.Json",
  "requestedVersion": "8.0.0",
  "projectFramework": "net48",
  "suggestedAlternatives": [
    {
      "packageId": "Newtonsoft.Json",
      "version": "13.0.3",
      "reason": "Compatible with net48"
    }
  ],
  "help": "Use the 'list_supported_frameworks' tool to see framework capabilities"
}
```

---

**Error Scenario 2: Build Validation Failure with Auto-Rollback**

```json
{
  "success": false,
  "errorCode": "BUILD_VALIDATION_FAILED",
  "category": "ExecutionError",
  "message": "Project failed to build after package update. Changes rolled back automatically.",
  "operation": "update Newtonsoft.Json to 13.0.3",
  "buildErrors": [
    {
      "code": "CS0246",
      "message": "The type or namespace 'JToken' could not be found",
      "file": "Controllers/ApiController.cs",
      "line": 42
    }
  ],
  "rollbackPerformed": true,
  "backup": "MyProject.csproj.backup",
  "help": "Package update may have introduced breaking API changes. Review release notes at https://github.com/JamesNK/Newtonsoft.Json/releases"
}
```

---

**Error Scenario 3: Conflict Resolution Required**

```json
{
  "success": false,
  "errorCode": "PACKAGE_VERSION_CONFLICTS",
  "category": "ValidationError",
  "message": "Version conflicts detected. Specify conflictResolutionStrategy to proceed.",
  "conflicts": [
    {
      "packageId": "Newtonsoft.Json",
      "versions": ["10.0.3", "12.0.3", "13.0.1"],
      "projects": ["ProjectA.csproj", "ProjectB.csproj", "ProjectC.csproj"]
    }
  ],
  "availableStrategies": ["highest", "manual", "fail"],
  "recommendation": "Use 'highest' to auto-select 13.0.1, or 'manual' to specify version",
  "help": "Re-invoke with conflictResolutionStrategy: 'highest' parameter"
}
```

---

## Success Metrics

### Adoption Metrics

**Primary KPI:** Active Users Per Month
- **Target:** 100 active users by Month 3, 500 by Month 6
- **Measurement:** Unique users invoking MCP tools (MCP server telemetry, opt-in)

**Tool Distribution:**
- **Hypothesis:** Package Management (50%) > CPM Migration (30%) > SDK Migration (20%)
- **Measurement:** Tool invocation counts per refactoring type

**Batch Operation Adoption:**
- **Target:** ≥40% of Package Management operations use `applyToAllProjects: true`
- **Measurement:** Parameter analysis in MCP invocations

---

### Value Delivery Metrics

**Time Saved Per Operation:**

| Refactoring | Baseline (Manual) | Target (Automated) | Improvement |
|-------------|-------------------|-------------------|-------------|
| Package Management (10 projects) | 2 hours | 5 minutes | 23x |
| SDK Migration (per project) | 4 hours | 30 minutes | 8x |
| CPM Migration (10 projects) | 90 minutes | 10 minutes | 9x |

**Measurement:** User surveys (quarterly)

**Cumulative Time Saved:**
- **Target:** 1,000+ developer hours saved by Month 6 across all users
- **Calculation:** (Average operations per user × Time savings per operation × Active users)

---

### Quality Metrics

**Refactoring Success Rate:**
- **Target:** ≥95% operations complete without errors
- **Measurement:** MCP server telemetry (success vs. error responses)

**Build Validation Rate:**
- **Target:** ≥98% post-refactoring build success
- **Measurement:** `dotnet build` validation pass/fail logs

**Rollback Rate:**
- **Baseline:** <5% operations require rollback
- **Measurement:** Auto-rollback invocations per total operations

---

### User Satisfaction Metrics

**Net Promoter Score (NPS):**
- **Target:** NPS ≥30 by Month 6 (considered "good" for developer tools)
- **Measurement:** In-tool survey after 10 successful refactorings
- **Question:** "How likely are you to recommend RefactorCsharpMCP's project file refactoring to a colleague?"

**Error Resolution Time:**
- **Target:** ≥80% of errors resolved within 10 minutes
- **Measurement:** Time from error response to retry/success (indicates clear error messages)

**User-Reported Issues:**
- **Target:** <1 critical bug per 100 operations
- **Measurement:** GitHub issue tracker (bug severity classification)

---

## Open Questions

### Question 1: Should V1.0 Support Multi-Targeting Projects?

**Context:**
Multi-targeting projects use `<TargetFrameworks>` (plural) to build for multiple frameworks (e.g., `net8.0;net48;netstandard2.0`).

**Product Implications:**
- **Complexity:** Package versions may differ per framework (System.Text.Json for net8.0, Newtonsoft.Json for net48)
- **UX:** Should tool apply refactoring to EACH framework independently?
- **Error Handling:** What if refactoring succeeds for net8.0 but fails for net48?

**Options:**
1. **V1.0: Single-Framework Only** (safer, simpler)
   - Reject multi-targeting projects with error: "Multi-targeting not supported in V1.0"
   - Defer to V1.5
2. **V1.0: Multi-Framework Support** (riskier, higher value)
   - Apply refactoring to each framework independently
   - Return per-framework results: `{ net8.0: success, net48: failed }`

**Recommendation:** Option 1 (Single-Framework Only) for V1.0
- **Rationale:** Most projects (>80%) use single target framework. Multi-targeting is edge case.
- **Mitigation:** Clear error message guides users to create separate projects for each framework.

**Decision Required By:** PRD approval (this week)

---

### Question 2: Should Dry-Run Be Required (Opt-Out) or Optional (Opt-In)?

**Context:**
Dry-run mode shows preview without modifying files.

**Product Implications:**
- **Safety:** Opt-out (default `dryRun: true`) prevents accidental file modification
- **Velocity:** Opt-in (default `dryRun: false`) reduces friction for experienced users

**Options:**
1. **Opt-In:** `dryRun: false` by default (current design)
   - Pro: Faster workflow for AI agents and experienced users
   - Con: Higher risk of unintended changes
2. **Opt-Out:** `dryRun: true` by default
   - Pro: Safer for new users
   - Con: Requires two invocations per operation (preview → execute)

**Recommendation:** Option 1 (Opt-In) with prominent warnings
- **Rationale:** AI agents and MCP tools are typically deterministic. Dry-run on every operation doubles latency.
- **Mitigation:** Add prominent warning to destructive operations: "⚠️ Always review changes before committing."

**Decision Required By:** Implementation planning (Week 1)

---

### Question 3: Should CPM Conflict Resolution Default to "Fail" or "Highest"?

**Context:**
When enabling CPM, version conflicts are common (ProjectA uses v10.0.3, ProjectB uses v13.0.1).

**Product Implications:**
- **"Fail" Default:** Safer (user must explicitly choose resolution)
- **"Highest" Default:** Faster (auto-resolves to latest version)

**Options:**
1. **Default: "Fail"** (current design)
   - Pro: User explicitly acknowledges version changes
   - Con: Requires two invocations (detect conflict → resolve conflict)
2. **Default: "Highest"**
   - Pro: One-click CPM enablement
   - Con: Silent version upgrades may break builds

**Recommendation:** Option 1 (Default: "Fail")
- **Rationale:** Package version upgrades can introduce breaking changes. Require explicit opt-in.
- **Mitigation:** Error message guides user to retry with `conflictResolutionStrategy: "highest"`

**Decision Required By:** Implementation planning (Week 1)

---

## Dependencies and Risks

### Internal Dependencies

**Dependency 1: Roslyn Compilation Context Factory (from Framework-Aware PRD)**
- **Requirement:** Existing infrastructure for creating framework-specific `CSharpCompilation` instances
- **Status:** Implemented in Phase 0 of Framework-Aware refactoring
- **Impact:** Project file refactorings need framework validation (e.g., package compatibility)
- **Mitigation:** Reuse existing `CompilationContextBuilder` from Framework-Aware infrastructure

**Dependency 2: Reference Assembly Management**
- **Requirement:** Cached reference assemblies for .NET Framework 4.6.2+, .NET 8-9, .NET Standard 2.0-2.1
- **Status:** Implemented in Phase 0 of Framework-Aware refactoring
- **Impact:** Package compatibility validation requires framework metadata
- **Mitigation:** Reuse existing `ReferenceAssemblyResolver` with NuGet package strategy

**Dependency 3: MCP SDK Updates**
- **Requirement:** ModelContextProtocol SDK 0.4.0+ for JSON schema generation
- **Status:** Already integrated (current version: 0.4.0-preview.1)
- **Impact:** Tool signature generation, parameter validation
- **Mitigation:** No action required (dependency satisfied)

---

### External Dependencies

**Dependency 4: MSBuild APIs**
- **Requirement:** Microsoft.Build.* packages for project evaluation and semantic understanding
- **Risk:** MSBuild evaluation complexity (dynamic properties, conditions, imports)
- **Mitigation:** Use MSBuild API for semantic evaluation, not just XML parsing. Validate with `dotnet build --no-restore` after every refactoring.

**Dependency 5: NuGet Client API**
- **Requirement:** NuGet.Protocol, NuGet.Packaging for package metadata and compatibility validation
- **Risk:** NuGet API rate limiting, private feed authentication
- **Mitigation:** Implement local caching, support for offline scenarios (optional validation)

**Dependency 6: Solution File Parsing**
- **Requirement:** Microsoft.Build.Construction.SolutionFile for .sln parsing (non-XML format)
- **Risk:** Solution file format fragility (custom text format, not XML)
- **Mitigation:** Always create backup before modification, validate with `dotnet sln list` after changes

---

### Technical Risks

**Risk 1: MSBuild Evaluation Complexity**

| Aspect | Risk Level | Impact | Probability | Mitigation |
|--------|-----------|--------|-------------|------------|
| Description | High | High | Medium | MSBuild properties are evaluated dynamically with conditions, imports, and inheritance. Simple XML parsing may miss effective values. |
| Example | Property value differs at evaluation time vs. raw XML | Build breaks | 30% | Use Microsoft.Build API for semantic evaluation. Test with real-world projects. Provide dry-run mode. |
| **Mitigation Strategy** | - Use Microsoft.Build.Evaluation.Project for property resolution<br>- Use MSBuild.Locator to resolve SDK paths<br>- Validate with `dotnet build --no-restore` after refactoring<br>- Provide detailed error logs for debugging |

---

**Risk 2: Solution File Format Fragility**

| Aspect | Risk Level | Impact | Probability | Mitigation |
|--------|-----------|--------|-------------|------------|
| Description | Critical | Critical | Medium | Solution file format is custom text (not XML). Small errors can corrupt entire solution. |
| Example | Missing GUID or malformed section breaks Visual Studio | Solution won't open | 20% | Use Microsoft.Build.Construction.SolutionFile API. Always backup. Validate. |
| **Mitigation Strategy** | - Use Microsoft.Build.Construction.SolutionFile API (NOT string manipulation)<br>- Always create .sln.backup before modification<br>- Validate with `dotnet sln list` after changes<br>- Provide rollback mechanism<br>- Test with VS 2022, Rider, VS Code |

---

**Risk 3: Framework Compatibility False Positives**

| Aspect | Risk Level | Impact | Probability | Mitigation |
|--------|-----------|--------|-------------|------------|
| Description | High | High | Medium | Detecting API compatibility is complex. False positives block valid upgrades, false negatives cause runtime errors. |
| Example | Tool incorrectly reports API as incompatible | User loses trust | 25% | Provide detailed incompatibility reports with source locations. Allow user overrides. Test known migration scenarios. |
| **Mitigation Strategy** | - Use Roslyn semantic analysis with proper reference assemblies<br>- Provide confidence scoring (High/Medium/Low)<br>- Allow user overrides with explicit acknowledgment<br>- Test against known migration paths (net48 → net6.0 → net8.0)<br>- Defer to V1.5 for full implementation (V1.0 basic validation only) |

---

**Risk 4: Package Version Conflicts**

| Aspect | Risk Level | Impact | Probability | Mitigation |
|--------|-----------|--------|-------------|------------|
| Description | Medium | Medium | High | Automatically resolving version conflicts may choose incorrect version (e.g., breaks API compatibility). |
| Example | ProjectA needs Newtonsoft.Json 12.0.3 for API compatibility, but tool upgrades to 13.0.1 | Build succeeds but runtime errors | 40% | Default to "fail" strategy (user chooses). Warn user. Suggest testing. |
| **Mitigation Strategy** | - Default conflictResolutionStrategy: "fail" (safest)<br>- Provide conflict report with affected projects<br>- Allow manual version selection<br>- Suggest testing after migration<br>- Document breaking changes per package version |

---

### Product Risks

**Risk 5: User Expectation Mismatch - Cross-File Operations**

| Aspect | Risk Level | Impact | Probability | Mitigation |
|--------|-----------|--------|-------------|------------|
| Description | High | Medium | High | Users expect project refactorings to work across entire solution automatically. |
| Example | User expects "Update package" to work on all 20 projects, but tool requires explicit `applyToAllProjects: true` | User frustration | 60% | Clear documentation. Provide batch mode. Default to solution-wide for common operations. |
| **Mitigation Strategy** | - **Add batch mode** to MVP scope for Package Management and Property Sync<br>- Clear tool descriptions: "Applies to single project by default. Use applyToAllProjects: true for solution-wide operation."<br>- AI agent guidance: Suggest batch mode when detecting multi-project solutions<br>- Examples in documentation showing batch usage |

---

**Risk 6: Build Breakage Liability**

| Aspect | Risk Level | Impact | Probability | Mitigation |
|--------|-----------|--------|-------------|------------|
| Description | Critical | Critical | Medium | Project file refactorings directly affect build systems. Failures are highly visible and block all work. |
| Example | SDK migration breaks build due to missing implicit excludes | Production deployment blocked | 15% | Backup/rollback, dry-run mode, post-refactoring validation, liability disclaimer. |
| **Mitigation Strategy** | - **Automatic backup** (.csproj.backup) before modification<br>- **Dry-run mode** for preview (default for destructive operations in docs)<br>- **Build validation** (`dotnet build`) after refactoring<br>- **Auto-rollback** on build failure<br>- **Liability disclaimer** in tool output: "⚠️ Always review changes before committing. Test builds locally." |

---

## Timeline and Milestones

### Phase 0: Infrastructure and Discovery (Weeks 1-2)

**Goal:** Establish shared infrastructure for project file manipulation and validation

**Week 1:**
- **Infrastructure Design** (3 days)
  - Design `ProjectFileLoader` (XDocument-based, format preservation)
  - Design `NuGetClientWrapper` (package metadata, framework compatibility)
  - Design `SolutionFileManager` (Microsoft.Build.Construction.SolutionFile)
  - Define error taxonomy for project file operations

- **Unit Test Foundation** (2 days)
  - Create test fixtures for .csproj manipulation
  - Sample project files (legacy, SDK-style, multi-framework)
  - NuGet package metadata mocks

**Week 2:**
- **Core Infrastructure Implementation** (4 days)
  - Implement `ProjectFileLoader` with backup/restore
  - Implement `NuGetClientWrapper` with caching
  - Implement `SolutionFileManager` with validation

- **Framework Validation Integration** (1 day)
  - Integrate with existing `FrameworkValidator` (from Framework-Aware PRD)
  - Package compatibility validation

**Phase 0 Deliverables:**
- ✅ `ProjectFileLoader` with backup/rollback
- ✅ `NuGetClientWrapper` with framework compatibility checking
- ✅ `SolutionFileManager` with .sln parsing
- ✅ Unit tests (30+ tests, >90% coverage)
- ✅ Error taxonomy documentation

---

### Phase 1: Package Reference Management (Weeks 3-4)

**Goal:** Implement highest-value refactoring (P0)

**Week 3:**
- **Core Logic** (3 days)
  - Add/update/remove package references (single project)
  - Framework compatibility validation
  - Build validation with rollback

- **Testing** (2 days)
  - Unit tests (15+ tests covering add/update/remove)
  - Integration tests (real .csproj files)
  - Error scenarios (incompatible frameworks, missing packages)

**Week 4:**
- **Batch Operations** (3 days)
  - Multi-project package management (`applyToAllProjects: true`)
  - Version conflict detection
  - Per-project status reporting

- **MCP Tool Integration** (2 days)
  - MCP tool signature and JSON schema
  - Error handling and response formatting
  - End-to-end tests with MCP SDK

**Phase 1 Deliverables:**
- ✅ `project_manage_package_reference` MCP tool
- ✅ Single-project and batch operations
- ✅ Framework compatibility validation
- ✅ Build validation with auto-rollback
- ✅ Unit + integration tests (25+ tests)
- ✅ Updated EXAMPLES.md

---

### Phase 2: SDK-Style Conversion (Weeks 5-6)

**Goal:** Implement critical modernization refactoring (P0)

**Week 5:**
- **Core Conversion Logic** (4 days)
  - Legacy → SDK-style transformation
  - TargetFrameworkVersion → TargetFramework mapping
  - PackageReference preservation/migration
  - Implicit vs. explicit include handling

- **Testing** (1 day)
  - Unit tests for conversion logic
  - Sample legacy projects (WinForms, Console, ClassLibrary)

**Week 6:**
- **ASP.NET Web App Handling** (2 days)
  - Detection and warning system
  - MSBuild.SDK.SystemWeb support (opt-in)

- **MCP Tool Integration** (2 days)
  - MCP tool signature
  - Dry-run mode with before/after diff
  - Build validation and rollback

- **Edge Case Testing** (1 day)
  - EmbeddedResource preservation
  - packages.config migration
  - Multi-project dependencies

**Phase 2 Deliverables:**
- ✅ `project_convert_to_sdk_style` MCP tool
- ✅ Legacy format support (VS2015 and earlier)
- ✅ ASP.NET Web App detection and warnings
- ✅ Dry-run preview mode
- ✅ Unit + integration tests (20+ tests)
- ✅ SDK migration guide (docs/sdk-migration-guide.md)

---

### Phase 3: Central Package Management (Weeks 7-8)

**Goal:** Implement architectural improvement refactoring (P1)

**Week 7:**
- **CPM Scaffolding** (3 days)
  - Create Directory.Build.props, Directory.Packages.props
  - Extract package versions from all projects
  - Remove Version attributes from .csproj files

- **Conflict Detection** (2 days)
  - Identify version conflicts across projects
  - Generate conflict reports

**Week 8:**
- **Conflict Resolution** (2 days)
  - Implement resolution strategies (fail, highest, manual)
  - Framework-specific version handling (Condition attributes)

- **MCP Tool Integration** (2 days)
  - MCP tool signature
  - Dry-run preview
  - Build validation

- **End-to-End Testing** (1 day)
  - Real multi-project solutions
  - Conflict scenarios

**Phase 3 Deliverables:**
- ✅ `project_enable_central_package_management` MCP tool
- ✅ Version conflict detection and resolution
- ✅ Framework-specific version handling
- ✅ Unit + integration tests (20+ tests)
- ✅ CPM migration guide (docs/cpm-migration-guide.md)

---

### Phase 4: Documentation and Release (Week 9)

**Goal:** Production-ready release with comprehensive documentation

**Week 9:**
- **Documentation** (3 days)
  - Updated README.md with project file refactorings
  - EXAMPLES.md with real-world scenarios
  - TROUBLESHOOTING.md for common issues
  - Migration guides (SDK, CPM)

- **Performance Benchmarking** (1 day)
  - Validate performance targets (<10s for 10-project operations)
  - Memory usage profiling

- **Release Preparation** (1 day)
  - Version tagging (v1.5.0)
  - Release notes
  - Docker image updates

**Phase 4 Deliverables:**
- ✅ Comprehensive documentation
- ✅ Performance benchmarks
- ✅ v1.5.0 release
- ✅ Announcement (GitHub Discussions, social media)

---

**Total Timeline:** 9 weeks (MVP launch)

**Post-V1.5 Roadmap:**
- V2.0: Update Target Framework with API compatibility analysis
- V2.0: Project Reference Management
- V2.0: Property Group Synchronization

---

## Appendix A: Framework Compatibility Validation

### Package Compatibility Matrix

RefactorCsharpMCP validates package compatibility using NuGet metadata and Target Framework Monikers (TFMs).

**Validation Logic:**

```
Given: packageId, version, targetFramework
1. Query NuGet API for package metadata
2. Extract supportedFrameworks from .nuspec
3. Check if targetFramework is in supportedFrameworks
4. If not compatible:
   - Return error with suggestedAlternatives
   - Example: System.Text.Json 8.0.0 requires net6.0+
   - Alternative: Newtonsoft.Json 13.0.3 for net48
```

**Example Compatibility Scenarios:**

| Package | Version | Target Framework | Compatible? | Alternative |
|---------|---------|------------------|-------------|-------------|
| System.Text.Json | 8.0.0 | net8.0 | ✅ Yes | - |
| System.Text.Json | 8.0.0 | net48 | ❌ No | Newtonsoft.Json 13.0.3 |
| Newtonsoft.Json | 13.0.3 | net8.0 | ✅ Yes | - |
| Newtonsoft.Json | 13.0.3 | net48 | ✅ Yes | - |
| Microsoft.Extensions.Logging | 8.0.0 | net8.0 | ✅ Yes | - |
| Microsoft.Extensions.Logging | 8.0.0 | net462 | ❌ No | Microsoft.Extensions.Logging 6.0.0 |

---

## Appendix B: Error Code Taxonomy

### Project File Refactoring Error Codes

All errors include standardized `errorCode` field for programmatic handling:

| Error Code | Category | HTTP Analogy | Description |
|------------|----------|--------------|-------------|
| `FRAMEWORK_INCOMPATIBILITY` | ValidationError | 400 Bad Request | Package version incompatible with target framework |
| `PACKAGE_NOT_FOUND` | ValidationError | 404 Not Found | Package ID not found on configured NuGet sources |
| `INVALID_VERSION_FORMAT` | ValidationError | 400 Bad Request | Package version is not valid SemVer |
| `PACKAGE_VERSION_CONFLICTS` | ValidationError | 409 Conflict | Multiple projects use different package versions |
| `BUILD_VALIDATION_FAILED` | ExecutionError | 422 Unprocessable | Project failed to build after refactoring |
| `WEB_APP_REQUIRES_MANUAL_REVIEW` | ValidationError | 400 Bad Request | ASP.NET Web App requires specialized SDK |
| `SOLUTION_FILE_CORRUPT` | ExecutionError | 500 Internal Error | Solution file format is invalid or unreadable |
| `MISSING_REQUIRED_FILE` | ValidationError | 404 Not Found | Required file (e.g., .csproj, .sln) not found at path |
| `CONCURRENT_MODIFICATION` | ExecutionError | 409 Conflict | File was modified externally during refactoring |

**See Also:**
- [PRD-Framework-Version-Awareness.md](PRD-Framework-Version-Awareness.md) - Framework validation error codes
- [TROUBLESHOOTING.md](../TROUBLESHOOTING.md) - Error resolution guide

---

## Document Approval

**Product Owner:** Approved - Ready for Implementation Planning (v1.0.0)
**Supporting Research:** [project-file-refactoring-analysis.md](project-file-refactoring-analysis.md) - Comprehensive technical analysis
**Commentary:** [commentary-project-file-refactoring-analysis.md](commentary-project-file-refactoring-analysis.md) - Product review and recommendations

**Date:** 2025-11-02
**Version:** 1.0.0
**Status:** Draft - Pending Stakeholder Review
**Next Review:** After stakeholder feedback (Week 1)

**Key Decisions Made:**
1. ✅ MVP scope: 3 refactorings (Package Management, SDK Migration, CPM)
2. ✅ Tier 1 priority: Package Management (P0), SDK Migration (P0), CPM (P1)
3. ✅ Conflict resolution default: "fail" (safest, requires explicit opt-in)
4. ✅ Dry-run mode: Opt-in (default `dryRun: false` for velocity)
5. ✅ Multi-targeting: Defer to V1.5 (single-framework only in V1.0)
6. ✅ Success metrics: Adoption (500 users by M6), Value (23x time savings), Quality (95% success rate)

**Open Questions for Stakeholder Review:**
1. Should dry-run be opt-in or opt-out? (Recommendation: Opt-in)
2. Should CPM conflict resolution default to "fail" or "highest"? (Recommendation: "fail")
3. Should V1.0 support multi-targeting projects? (Recommendation: Defer to V1.5)

---

**END OF DOCUMENT**
