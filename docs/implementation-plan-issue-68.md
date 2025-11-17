# Implementation Plan for Issue #68: .NET Project File Refactoring

**Issue:** #68 - Implement .NET project file refactoring capabilities (MVP)
**Created:** 2025-11-17
**Status:** Planning Complete, Ready for Implementation

## Executive Summary

This plan outlines the implementation of .NET project file refactoring capabilities for RefactorCsharpMCP, extending its C# source code refactoring features to handle project files (.csproj, .sln, Directory.Build.props). The MVP will deliver three high-value refactorings that address critical developer pain points: Package Reference Management, SDK-Style Project Conversion, and Central Package Management Migration.

## 1. Architecture Overview

### 1.1 Overall Design Principles

- **Separation of Concerns**: Project file refactorings will be separate from C# code refactorings but share common infrastructure
- **XML-Based Processing**: Use `System.Xml.Linq` (XDocument) for project file manipulation to preserve formatting
- **MSBuild Integration**: Leverage `Microsoft.Build` APIs for semantic evaluation and validation
- **Defensive Programming**: Always create backups, validate builds, and provide rollback capabilities
- **Framework Awareness**: Reuse existing `FrameworkValidator` and `CompilationContextBuilder` infrastructure

### 1.2 Project Structure

```
src/RefactorCsharpMCP.Core/
├── ProjectFiles/                        # New namespace for project file operations
│   ├── Infrastructure/
│   │   ├── ProjectFileLoader.cs         # XDocument-based project file loading/saving
│   │   ├── ProjectFileBackup.cs         # Backup and rollback management
│   │   ├── BuildValidator.cs            # Post-refactoring build validation
│   │   └── ProjectFileConstants.cs      # XML namespaces, element names
│   │
│   ├── NuGet/
│   │   ├── NuGetClientWrapper.cs        # NuGet API integration
│   │   ├── PackageCompatibilityAnalyzer.cs  # Framework compatibility checking
│   │   └── PackageVersionResolver.cs    # Version conflict resolution
│   │
│   ├── Solution/
│   │   ├── SolutionFileManager.cs       # .sln file parsing and manipulation
│   │   └── ProjectDiscovery.cs          # Find all projects in solution
│   │
│   ├── Refactorings/
│   │   ├── ProjectRefactoringBase.cs    # Base class extending RefactoringBase
│   │   ├── PackageReferenceManager.cs   # Add/update/remove packages
│   │   ├── SdkStyleConverter.cs         # Legacy to SDK-style conversion
│   │   └── CentralPackageManagement.cs  # CPM enablement
│   │
│   └── Models/
│       ├── ProjectFileContext.cs        # Project file metadata
│       ├── PackageReference.cs          # Package reference model
│       └── ProjectRefactoringOptions.cs # Options for project refactorings

src/RefactorCsharpMCP.Server/
└── Tools/
    ├── ProjectManagePackageReferenceTool.cs
    ├── ProjectConvertToSdkStyleTool.cs
    └── ProjectEnableCentralPackageManagementTool.cs

src/RefactorCsharpMCP.Tests/
└── ProjectFiles/
    ├── Infrastructure/
    ├── NuGet/
    ├── Refactorings/
    └── TestData/
        ├── LegacyProjects/
        └── ModernProjects/
```

## 2. Shared Infrastructure Components

### 2.1 ProjectRefactoringBase Class

```csharp
public abstract class ProjectRefactoringBase : RefactoringBase
{
    protected ProjectFileLoader FileLoader { get; }
    protected BuildValidator BuildValidator { get; }
    protected NuGetClientWrapper NuGetClient { get; }

    // Common validation for project file operations
    protected RefactoringResult ValidateProjectFile(string projectPath);

    // Backup and rollback support
    protected string CreateBackup(string filePath);
    protected void Rollback(string backupPath, string originalPath);

    // Build validation with auto-rollback
    protected async Task<RefactoringResult> ValidateBuildWithRollbackAsync(
        string projectPath, string backupPath);

    // Multi-file operation tracking
    protected class BatchOperationResult
    {
        public List<string> FilesModified { get; set; }
        public Dictionary<string, string> FilesFailed { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }
}
```

### 2.2 ProjectFileLoader

```csharp
public class ProjectFileLoader
{
    // Load with format preservation
    public XDocument LoadProject(string path);

    // Save with optional formatting
    public void SaveProject(XDocument doc, string path, bool preserveFormatting = true);

    // Detect project type (SDK-style vs legacy)
    public ProjectType DetectProjectType(XDocument doc);

    // Extract target framework(s)
    public List<string> GetTargetFrameworks(XDocument doc);
}
```

### 2.3 NuGetClientWrapper

```csharp
public class NuGetClientWrapper
{
    private readonly ILogger<NuGetClientWrapper> _logger;
    private readonly IPackageSearchMetadata _metadataCache;

    // Get package metadata with caching
    public Task<PackageMetadata> GetPackageMetadataAsync(
        string packageId, string version);

    // Check framework compatibility
    public Task<bool> IsCompatibleWithFrameworkAsync(
        string packageId, string version, string targetFramework);

    // Get latest stable version
    public Task<string> GetLatestVersionAsync(
        string packageId, bool includePrerelease = false);
}
```

## 3. Refactoring Implementations

### 3.1 Package Reference Management (P0)

**Implementation Details:**

```csharp
public class PackageReferenceManager : ProjectRefactoringBase
{
    public async Task<RefactoringResult> ManagePackageReferenceAsync(
        string projectPath,
        PackageOperation operation,
        string packageId,
        string? version,
        bool applyToAllProjects,
        string? targetFramework,
        bool dryRun,
        bool validateBuild)
    {
        // 1. Input validation
        // 2. Load project(s)
        // 3. Framework compatibility check (if adding/updating)
        // 4. Perform XML manipulation
        // 5. Save with backup
        // 6. Build validation with rollback
        // 7. Return BatchOperationResult
    }

    private XElement CreatePackageReference(string packageId, string version);
    private void UpdatePackageVersion(XElement packageRef, string version);
    private bool RemovePackageReference(XDocument doc, string packageId);
}
```

**Key Features:**
- Single and batch operations (`applyToAllProjects`)
- Framework compatibility validation via NuGet API
- Automatic backup and rollback
- Dry-run preview mode
- Version conflict detection for batch operations

### 3.2 SDK-Style Project Conversion (P0)

**Implementation Details:**

```csharp
public class SdkStyleConverter : ProjectRefactoringBase
{
    public async Task<RefactoringResult> ConvertToSdkStyleAsync(
        string projectPath,
        bool dryRun,
        bool validateBuild,
        bool allowWebApps)
    {
        // 1. Detect project type (Console, Library, WinForms, ASP.NET)
        // 2. Check for ASP.NET Web App (special handling)
        // 3. Extract essential properties
        // 4. Build new SDK-style project structure
        // 5. Migrate PackageReferences
        // 6. Handle implicit vs explicit includes
        // 7. Save and validate
    }

    private string MapTargetFramework(string legacyVersion);
    private void MigratePackagesConfig(XDocument legacyDoc, XDocument newDoc);
    private bool IsAspNetWebApp(XDocument doc);
    private XDocument CreateSdkStyleProject(ProjectMetadata metadata);
}
```

**Key Features:**
- Automatic detection of project type
- ASP.NET Web App warnings and special SDK handling
- packages.config migration
- Implicit includes optimization
- Before/after diff in dry-run mode

### 3.3 Central Package Management Migration (P1)

**Implementation Details:**

```csharp
public class CentralPackageManagement : ProjectRefactoringBase
{
    public async Task<RefactoringResult> EnableCpmAsync(
        string solutionPath,
        ConflictResolutionStrategy strategy,
        bool dryRun,
        bool validateBuild)
    {
        // 1. Discover all projects in solution
        // 2. Extract all package versions
        // 3. Detect and resolve conflicts
        // 4. Create Directory.Build.props
        // 5. Create Directory.Packages.props
        // 6. Update all project files
        // 7. Validate solution build
    }

    private Dictionary<string, List<VersionInfo>> ExtractPackageVersions(
        IEnumerable<string> projectPaths);

    private Dictionary<string, string> ResolveVersionConflicts(
        Dictionary<string, List<VersionInfo>> versions,
        ConflictResolutionStrategy strategy);

    private XDocument CreateDirectoryBuildProps();
    private XDocument CreateDirectoryPackagesProps(
        Dictionary<string, string> packageVersions);
}
```

**Key Features:**
- Automatic version extraction from all projects
- Configurable conflict resolution strategies
- Framework-specific version handling with conditions
- Multi-file transaction support
- Comprehensive preview in dry-run mode

## 4. MCP Tool Integration

### 4.1 Tool Schema Definitions

```csharp
[McpServerToolType]
public class ProjectManagePackageReferenceTool
{
    [McpServerTool]
    [Description("Add, update, or remove NuGet package references...")]
    public async Task<object> ProjectManagePackageReference(
        [Description("Absolute path to .csproj or solution directory")] string projectPath,
        [Description("Operation: 'add', 'update', or 'remove'")] string operation,
        [Description("NuGet package identifier")] string packageId,
        [Description("Package version (for add/update)")] string? version = null,
        [Description("Apply to all projects in solution")] bool applyToAllProjects = false,
        [Description("Target framework for validation")] string? targetFramework = null,
        [Description("Preview changes without modifying")] bool dryRun = false,
        [Description("Validate build after operation")] bool validateBuild = true)
    {
        // Validate inputs using ToolInputValidator
        // Create options object
        // Call PackageReferenceManager
        // Format response for MCP
    }
}
```

### 4.2 Response Format

```json
{
  "success": true,
  "operation": "update",
  "packageId": "Newtonsoft.Json",
  "version": "13.0.3",
  "filesModified": [
    "/path/to/ProjectA.csproj",
    "/path/to/ProjectB.csproj"
  ],
  "filesSkipped": [
    {
      "path": "/path/to/ProjectC.csproj",
      "reason": "Package not present"
    }
  ],
  "buildValidation": "PASSED",
  "executionTime": "5.2s",
  "warnings": [],
  "backup": "/path/to/ProjectA.csproj.backup"
}
```

## 5. Testing Strategy

### 5.1 Unit Tests

- **Infrastructure Tests**: ProjectFileLoader, NuGetClientWrapper, BuildValidator
- **Refactoring Logic Tests**: Each refactoring with various scenarios
- **Edge Cases**: Malformed XML, missing files, network failures
- **Coverage Target**: >90% for core logic

### 5.2 Integration Tests

```csharp
[Fact]
public async Task PackageManagement_UpdatesAcrossMultipleProjects()
{
    // Create test solution with 3 projects
    // Each with different package versions
    // Run update operation
    // Verify all projects updated
    // Verify build succeeds
}

[Fact]
public async Task SdkConversion_HandlesAspNetWebApps()
{
    // Load legacy ASP.NET project
    // Attempt conversion
    // Verify warning returned
    // With allowWebApps=true, verify conversion
}

[Fact]
public async Task CpmMigration_ResolvesVersionConflicts()
{
    // Create solution with conflicting versions
    // Run CPM enablement
    // Verify conflict resolution
    // Verify all projects build
}
```

### 5.3 Test Data

- **Legacy Projects**: Real-world legacy .csproj files from .NET Framework 4.x
- **Modern Projects**: SDK-style projects with various configurations
- **Edge Cases**: Multi-targeting, conditional references, custom MSBuild logic

## 6. Risk Mitigation Strategies

### 6.1 Technical Risks

| Risk | Mitigation |
|------|------------|
| **MSBuild Evaluation Complexity** | Use Microsoft.Build API for semantic evaluation, not just XML parsing |
| **Solution File Corruption** | Always backup, use Microsoft.Build.Construction API, validate with `dotnet sln list` |
| **Package Compatibility False Positives** | Query NuGet API for accurate metadata, provide override options |
| **Build Breakage** | Automatic rollback on validation failure, dry-run mode default for destructive ops |

### 6.2 User Experience Risks

| Risk | Mitigation |
|------|------------|
| **Multi-file Operation Confusion** | Clear per-file status reporting, transaction semantics |
| **Version Conflict Handling** | Default to "fail" strategy, require explicit resolution choice |
| **Unexpected Behavior** | Comprehensive documentation, examples, warning messages |

## 7. Implementation Phases

### Phase 0: Infrastructure (Weeks 1-2)
- Implement ProjectFileLoader with backup/restore
- Implement NuGetClientWrapper with caching
- Implement BuildValidator with dotnet CLI integration
- Create ProjectRefactoringBase extending RefactoringBase
- Unit tests for all infrastructure components

### Phase 1: Package Management (Weeks 3-4)
- Implement PackageReferenceManager
- Add single-project operations
- Add batch operations with conflict detection
- Implement MCP tool wrapper
- Integration tests with real packages

### Phase 2: SDK Migration (Weeks 5-6)
- Implement SdkStyleConverter
- Add project type detection
- Handle ASP.NET Web Apps
- Implement packages.config migration
- Edge case testing

### Phase 3: CPM Migration (Weeks 7-8)
- Implement CentralPackageManagement
- Add conflict resolution strategies
- Create Directory.*.props generation
- Multi-project coordination
- End-to-end testing

### Phase 4: Polish & Release (Week 9)
- Documentation (EXAMPLES.md updates)
- Performance optimization
- Error message refinement
- Release preparation

## 8. Success Metrics

### Primary KPIs
- **Adoption**: 100 active users by Month 3, 500 by Month 6
- **Time Savings**: 23x for package management, 8x for SDK migration, 9x for CPM
- **Quality**: ≥95% success rate, ≥98% build validation rate
- **Satisfaction**: NPS ≥30 by Month 6

### Feature Metrics
- Tool invocation distribution (expected: Package > CPM > SDK)
- Batch operation adoption (target: ≥40% use `applyToAllProjects`)
- Dry-run usage (indicates user caution/trust level)
- Rollback frequency (indicates reliability)

## 9. Key Technical Decisions

### 9.1 XML vs MSBuild API
- **Decision**: Use XML (XDocument) for manipulation, MSBuild for validation
- **Rationale**: XML preserves formatting, MSBuild provides semantic evaluation

### 9.2 Separate vs Integrated Tools
- **Decision**: Separate MCP tools with "project_" prefix
- **Rationale**: Clear separation of concerns, better discoverability

### 9.3 Default Behaviors
- **Conflict Resolution**: Default to "fail" (safest)
- **Build Validation**: Default to true (safety over speed)
- **Dry Run**: Default to false (efficiency for AI agents)
- **Backup**: Always create (no option to disable)

### 9.4 Framework Integration
- **Decision**: Reuse existing FrameworkValidator and CompilationContextBuilder
- **Rationale**: Consistency with C# refactorings, avoid duplication

## 10. Documentation Requirements

### User Documentation
- Update README.md with new tools
- Add comprehensive examples to EXAMPLES.md
- Create migration guides (SDK, CPM)
- Add troubleshooting section

### Technical Documentation
- XML schema assumptions
- MSBuild property evaluation notes
- NuGet API usage patterns
- Performance characteristics

## 11. Future Enhancements (Post-MVP)

- **Update Target Framework**: With API compatibility analysis
- **Project Reference Management**: Add/remove with path resolution
- **Property Group Synchronization**: Ensure consistency
- **Solution File Operations**: Add/remove projects
- **Multi-targeting Support**: Handle complex scenarios
- **Visual Studio Integration**: Beyond MCP protocol

## 12. Open Product Decisions

### 12.1 Dry-Run Mode Default
- **Question:** Should dry-run be opt-in (`dryRun: false`) or opt-out (`dryRun: true`)?
- **Recommendation:** Opt-in for velocity (AI agents prefer deterministic execution)
- **Trade-off:** Safety vs. latency (dry-run doubles invocation count)
- **Status:** ⚠️ Requires stakeholder decision

### 12.2 Conflict Resolution Strategy
- **Question:** Should CPM conflict resolution default to "fail" or "highest"?
- **Recommendation:** "fail" for safety (require explicit version upgrade acknowledgment)
- **Trade-off:** Safety vs. one-click enablement
- **Status:** ⚠️ Requires stakeholder decision

### 12.3 Multi-Targeting Support
- **Question:** Should V1.0 support multi-targeting projects (`<TargetFrameworks>net8.0;net48</TargetFrameworks>`)?
- **Recommendation:** Defer to V1.5 (edge case, high complexity)
- **Trade-off:** Scope simplicity vs. comprehensive coverage
- **Status:** ⚠️ Requires stakeholder decision

## Conclusion

This implementation plan provides a comprehensive roadmap for adding project file refactoring capabilities to RefactorCsharpMCP. The phased approach minimizes risk while delivering high-value features early. The architecture leverages existing infrastructure while maintaining clear separation between C# code and project file operations. Success will be measured through adoption, time savings, and user satisfaction metrics.

---

**Next Steps:**
1. Resolve open product decisions (Section 12)
2. Begin Phase 0: Infrastructure implementation
3. Create detailed test specifications
4. Update project documentation
