---
description: Start work on the next highest-priority GitHub issue (or specific issues)
---

# INSTRUCTION FOR CLAUDE

This command supports two modes of operation:

## Mode 1: Explicit Ticket Numbers (Skip Priority Analysis)

**When ticket numbers are provided**, parse them from flexible input formats and work on those specific issues.

### Supported Input Formats:
- **Space-separated**: `/start-next 87 88 89`
- **Comma-separated**: `/start-next 87,88,89`
- **Comma with spaces**: `/start-next 87, 88, 89`
- **Ranges**: `/start-next 87-92` (expands to 87, 88, 89, 90, 91, 92)
- **Mixed**: `/start-next 87-89,92,95` (expands to 87, 88, 89, 92, 95)

### Parsing Algorithm:
```
1. Extract all arguments after /start-next command
2. Split by commas and spaces
3. For each token:
   - If contains "-" (e.g., "87-92"), expand range to individual numbers
   - If single number, add to list
4. Remove duplicates and sort numerically
5. Validate each ticket number
```

### Step 1: Parse and Validate Ticket Numbers

**REQUIRED** when ticket numbers are provided:

```bash
# Parse the input to extract all ticket numbers
# Example input: "87-89,92,95" or "87 88 89" or "87,88,89"
# Output: Array of unique ticket numbers [87, 88, 89, 92, 95]

# For each ticket number, validate it exists and is OPEN
for ticket in ${TICKET_NUMBERS[@]}; do
  gh issue view $ticket --json number,title,state,assignees 2>&1

  # Check if command succeeded
  if [ $? -ne 0 ]; then
    echo "❌ ERROR: Ticket #$ticket does not exist or is not accessible"
    INVALID_TICKETS+=($ticket)
  else
    # Check if ticket is OPEN
    STATE=$(gh issue view $ticket --json state --jq '.state')
    if [ "$STATE" != "OPEN" ]; then
      echo "⚠️  WARNING: Ticket #$ticket is $STATE (not OPEN)"
      CLOSED_TICKETS+=($ticket)
    else
      echo "✅ Ticket #$ticket is valid and OPEN"
      VALID_TICKETS+=($ticket)
    fi
  fi
done

# Report validation results
if [ ${#INVALID_TICKETS[@]} -gt 0 ]; then
  echo ""
  echo "❌ VALIDATION FAILED - Invalid tickets found:"
  for ticket in ${INVALID_TICKETS[@]}; do
    echo "   - #$ticket (does not exist or not accessible)"
  done
  echo ""
  echo "Please verify ticket numbers and try again."
  exit 1
fi

if [ ${#CLOSED_TICKETS[@]} -gt 0 ]; then
  echo ""
  echo "⚠️  WARNING - Some tickets are not OPEN:"
  for ticket in ${CLOSED_TICKETS[@]}; do
    STATE=$(gh issue view $ticket --json state --jq '.state')
    echo "   - #$ticket (state: $STATE)"
  done
  echo ""
  echo "These tickets will be skipped. Continue with remaining ${#VALID_TICKETS[@]} tickets?"
  # Inform user and ask if they want to continue
fi

if [ ${#VALID_TICKETS[@]} -eq 0 ]; then
  echo ""
  echo "❌ No valid OPEN tickets to work on."
  echo "Please provide at least one valid OPEN ticket number."
  exit 1
fi

echo ""
echo "✅ Validation complete: ${#VALID_TICKETS[@]} valid OPEN tickets"
echo "Working on tickets: ${VALID_TICKETS[@]}"
```

### Step 2: Invoke GitHub Issue Workflow for Each Ticket

**After validation**, invoke the `github-issue-workflow` skill for each valid ticket with explicit instructions:

```
For each ticket in VALID_TICKETS:
1. Set context: "Work on ticket #$ticket specifically"
2. Invoke github-issue-workflow skill with instruction to work on ticket #$ticket
3. The skill will:
   - Skip priority query (ticket already specified)
   - Review ticket #$ticket details
   - Create feature branch for ticket #$ticket
   - Assign ticket to user
   - Add in-progress label
   - Post status comment
   - Begin work on ticket #$ticket

4. After completing ticket #$ticket, move to next ticket in list
```

### Branch Naming for Multiple Tickets:

When working on **multiple tickets in one session**, create a single branch that includes:
- All ticket numbers (or range if consecutive)
- Tight description summarizing the common theme

**Examples:**
- Tickets 87, 88, 89: `feature/87-89-solid-validator-refactor`
- Tickets 87, 90, 92: `feature/87-90-92-solid-refactoring`
- Single ticket 87: `feature/87-solid-refactor-syntax-validator`

**Branch name format**: `<type>/<ticket-range>-<tight-description>`
- `<type>`: feature, fix, refactor, docs
- `<ticket-range>`: Single number, hyphenated range, or comma-separated
- `<tight-description>`: 2-4 words max, kebab-case

---

## Mode 2: Automatic Priority Selection (Default Behavior)

**When NO ticket numbers are provided**, use the original two-step workflow:

### Pre-Check: Detect Recent Top5 Analysis

**FIRST**, check the recent conversation history (last 3-5 messages) for evidence of a top5 issues analysis:
- Look for a "Top N Priority Issues" heading or similar
- Look for issue rankings with scores (e.g., "#234 - Fix authentication (Score: 165)")
- Look for prioritization criteria or score breakdowns

**If found**: The user has already reviewed priorities. Skip Step 1 and proceed directly to Step 2.

**If NOT found**: Execute both Step 1 and Step 2 sequentially.

### Step 1: Analyze Top Priority Issues (Conditional)

**Only execute if NO recent top5 analysis was found in conversation history.**

Invoke the `top-issues-analysis` skill using the Skill tool to display the top 5 priority issues.

This provides visibility into:
- Current priority rankings with scores
- Issue metadata (labels, milestones, dependencies)
- Reasoning for prioritization
- Overall insights and recommendations

**Wait for the analysis to complete before proceeding to Step 2.**

### Step 2: Select and Start Work on Top Issue (Always Execute)

Invoke the `github-issue-workflow` skill using the Skill tool.

The skill will autonomously:
1. Query GitHub Issues with priority labels (priority-1, priority-2, priority-3)
2. Analyze milestones, dependencies, and blocking issues
3. Select the highest-priority unassigned issue
4. Review issue details and requirements
5. Prepare the development environment
6. Create a properly named feature branch
7. Assign the issue to the user (MANDATORY)
8. Add "in-progress" label if it exists in repo (MANDATORY)
9. Post a status comment to the GitHub issue with branch name (MANDATORY)
10. Create a work plan for complex issues
11. Begin implementation

---

## Execution Logic

```
1. Parse command arguments after /start-next

2. IF arguments contain ticket numbers:
     a. Parse ticket numbers (handle commas, ranges, spaces)
     b. Validate all tickets exist and are OPEN
     c. Report validation results to user
     d. If validation fails, STOP and report errors
     e. If validation succeeds, invoke workflow for each ticket
     f. Create single branch for all tickets with proper naming

3. ELSE (no ticket numbers):
     a. Check for recent top5 analysis in conversation history
     b. If not found, invoke top-issues-analysis skill
     c. Invoke github-issue-workflow skill to select top priority issue
     d. Create branch following standard naming convention

4. Report completion status to user
```

---

**Execute the workflow now based on whether ticket numbers were provided.**
