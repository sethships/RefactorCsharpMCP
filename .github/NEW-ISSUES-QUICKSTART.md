# Quick Start: Creating New GitHub Issues

This guide helps you quickly create the 3 new issues identified in the grooming analysis.

## Overview

Three comprehensive issue descriptions are ready in `.github/issues/`:

1. **Issue #125**: Lambda Parameter Renaming (Bug/Enhancement)
2. **Issue #126**: NullableReferenceTypeStripper Enhancement (Enhancement)
3. **Issue #127**: Syntax Conversion Pipeline Converters (Tracking Issue)

## How to Create Issues

### Option 1: Copy-Paste from Files

1. Open the issue file: `.github/issues/issue-XXX.md`
2. Copy the entire content
3. Go to GitHub → https://github.com/sethb75/RefactorCsharpMCP/issues/new
4. Paste into the description field
5. Add the recommended labels (listed in file)
6. Set milestone (listed in file)
7. Click "Submit new issue"

### Option 2: Use GitHub CLI (if available)

```bash
# Issue #125
gh issue create \
  --title "Rename Symbol: Lambda Parameter Renaming Fails" \
  --body-file .github/issues/issue-125-lambda-parameter-renaming.md \
  --label "bug,refactoring: rename-symbol,priority: medium,roslyn,good first issue" \
  --milestone "V1.1 - Enhancements"

# Issue #126
gh issue create \
  --title "NullableReferenceTypeStripper: Add Semantic Analysis for Type Distinction" \
  --body-file .github/issues/issue-126-nullable-stripper-semantic-analysis.md \
  --label "enhancement,syntax-conversion,priority: low,framework-aware" \
  --milestone "V2.0 - Enhancements"

# Issue #127
gh issue create \
  --title "Syntax Conversion Pipeline: Implement Remaining Converters" \
  --body-file .github/issues/issue-127-syntax-conversion-pipeline-converters.md \
  --label "enhancement,syntax-conversion,priority: low,framework-aware,help wanted" \
  --milestone "V2.5+ - Future Enhancements"
```

## Issue Summaries

### Issue #125: Lambda Parameter Renaming

**Priority**: P1 (Medium) | **Effort**: 2-3 days | **Milestone**: V1.1

**Problem**: Rename Symbol fails on lambda parameters (test skipped).

**Labels**:
- `bug`
- `refactoring: rename-symbol`
- `priority: medium`
- `roslyn`
- `good first issue`

**Why Important**:
- Completes Rename Symbol feature
- Common in LINQ-heavy code
- Well-scoped investigation task
- Good for new contributors

**File**: `.github/issues/issue-125-lambda-parameter-renaming.md`

---

### Issue #126: NullableReferenceTypeStripper Enhancement

**Priority**: P2 (Low) | **Effort**: 1-2 days | **Milestone**: V2.0

**Problem**: Strips ALL `?` without distinguishing reference vs value types.

**Labels**:
- `enhancement`
- `syntax-conversion`
- `priority: low`
- `framework-aware`

**Why Important**:
- Semantic correctness
- Preserves value type nullables (`int?`)
- Framework-aware conversion accuracy

**Example**:
```csharp
// Current (incorrect)
string? name → string  // ✓ Correct
int? age → int         // ✗ Wrong, should be int?

// Proposed (correct)
string? name → string  // ✓ Correct
int? age → int?        // ✓ Correct
```

**File**: `.github/issues/issue-126-nullable-stripper-semantic-analysis.md`

---

### Issue #127: Syntax Conversion Pipeline Converters

**Priority**: P3 (Low) | **Effort**: Varies | **Milestone**: V2.5+

**Purpose**: Tracking issue for 8 potential syntax converters.

**Labels**:
- `enhancement`
- `syntax-conversion`
- `priority: low`
- `framework-aware`
- `help wanted`

**Why Important**:
- Documents future converter roadmap
- Establishes demand-driven criteria
- Aggregates user feedback
- Prevents speculative development

**Proposed Converters**:
1. CollectionExpressionConverter (7-10 days) - C# 12 → C# 11
2. PrimaryConstructorConverter (5-7 days) - C# 12 → C# 11
3. RecordToClassConverter (10-15 days) - C# 9 → C# 8
4. StringInterpolationConverter (3-4 days) - C# 6 → C# 5
5. ExpressionBodiedMemberConverter (2-3 days) - C# 6 → C# 5
6. SwitchExpressionConverter (4-6 days) - C# 8 → C# 7.3
7. PatternMatchingConverter (3-5 days) - C# 7 → C# 6
8. InitOnlySetterConverter (1-2 days) - C# 9 → C# 8

**Strategy**: Only implement when ≥5 user requests

**File**: `.github/issues/issue-127-syntax-conversion-pipeline-converters.md`

---

## Label Setup Required

Before creating issues, ensure these labels exist in your repository:

### Type Labels
- `bug`
- `enhancement`
- `feature`
- `documentation`

### Priority Labels
- `priority: critical`
- `priority: high`
- `priority: medium`
- `priority: low`

### Component Labels
- `refactoring: rename-symbol`
- `syntax-conversion`
- `framework-aware`
- `roslyn`
- `docker`

### Special Labels
- `good first issue`
- `help wanted`

### Create Labels via GitHub CLI

```bash
# Type labels
gh label create "bug" --color "d73a4a" --description "Something isn't working"
gh label create "enhancement" --color "a2eeef" --description "New feature or request"

# Priority labels
gh label create "priority: critical" --color "b60205" --description "P0 - Blocking"
gh label create "priority: high" --color "d93f0b" --description "P0-P1 - Important"
gh label create "priority: medium" --color "fbca04" --description "P1-P2 - Should do"
gh label create "priority: low" --color "0e8a16" --description "P2-P3 - Nice to have"

# Component labels
gh label create "refactoring: rename-symbol" --color "c5def5" --description "Rename Symbol refactoring"
gh label create "syntax-conversion" --color "c5def5" --description "Syntax conversion pipeline"
gh label create "framework-aware" --color "bfdadc" --description "Framework-specific behavior"
gh label create "roslyn" --color "e99695" --description "Roslyn API issue"

# Special labels
gh label create "good first issue" --color "7057ff" --description "Good for newcomers"
gh label create "help wanted" --color "008672" --description "Extra attention needed"
```

## Milestone Setup Required

Create these milestones in GitHub:

1. **V1.1 - Enhancements**
   - Description: Docker integration and feature polish
   - Due date: (Set based on your schedule)

2. **V2.0 - Enhancements**
   - Description: Cross-file refactoring and enhancements
   - Due date: (Set based on your schedule)

3. **V2.5+ - Future Enhancements**
   - Description: Advanced features and converters
   - Due date: No due date (backlog)

## After Creating Issues

1. **Update Issue Numbers** in:
   - `docs/ISSUE-GROOMING-ANALYSIS.md` (if GitHub assigns different numbers)
   - Any related documentation

2. **Link to Grooming Analysis**:
   Add comment to each issue:
   ```markdown
   This issue was created from the grooming analysis in
   docs/ISSUE-GROOMING-ANALYSIS.md
   ```

3. **Close Completed Issues**:
   Check issues #108-#124 and close if already merged

4. **Verify Issue #20**:
   Check if Remove Unused Usings is complete (per PRD)

## Validation Checklist

Before submitting each issue:

- [ ] Title is clear and descriptive
- [ ] Labels match recommendations
- [ ] Milestone is set
- [ ] Description includes:
  - [ ] Problem statement
  - [ ] Current behavior
  - [ ] Expected behavior
  - [ ] Code examples
  - [ ] Acceptance criteria
  - [ ] Effort estimate
  - [ ] Related issues/docs
- [ ] Issue is linked to grooming analysis

## Quick Links

- **Grooming Analysis**: `docs/ISSUE-GROOMING-ANALYSIS.md`
- **Issue #125 File**: `.github/issues/issue-125-lambda-parameter-renaming.md`
- **Issue #126 File**: `.github/issues/issue-126-nullable-stripper-semantic-analysis.md`
- **Issue #127 File**: `.github/issues/issue-127-syntax-conversion-pipeline-converters.md`
- **GitHub New Issue**: https://github.com/sethb75/RefactorCsharpMCP/issues/new

## Need Help?

If you encounter issues:

1. Check label names match exactly (case-sensitive)
2. Verify milestones exist before assigning
3. Ensure you have permission to create issues
4. Try GitHub web UI if CLI fails

---

**Created**: 2025-11-15
**From**: Issue Grooming Analysis (docs/ISSUE-GROOMING-ANALYSIS.md)
**Status**: Ready to create in GitHub
