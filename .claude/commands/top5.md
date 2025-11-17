---
description: Analyze and rank top priority GitHub issues
---

# INSTRUCTION FOR CLAUDE

Invoke the `top-issues-analysis` skill using the Skill tool.

## Parameters

- **Default**: Analyze top 5 issues
- **Custom count**: Parse the first argument as the count (e.g., `/top5 10` → count=10)
- **Valid range**: 1-50 issues

## Usage Examples

```
/top5       → Analyze top 5 issues (default)
/top5 3     → Analyze top 3 issues
/top5 10    → Analyze top 10 issues
```

## Execution

Pass the count parameter to the skill invocation:
- If no argument provided: invoke with default (5)
- If argument provided: invoke with that count

The skill will:
- Fetch and analyze open GitHub issues
- Calculate priority scores using multi-criteria algorithm
- Display top N issues with scores, metadata, and reasoning
- Provide actionable insights and recommendations

**Read-only**: No GitHub modifications are made.

---

**Execute the skill now with the appropriate count parameter.**
