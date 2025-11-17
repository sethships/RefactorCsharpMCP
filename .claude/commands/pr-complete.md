---
description: Clean up after a PR has been merged - update local repo and close linked issues
---

# INSTRUCTION FOR CLAUDE

Invoke the `pr-completion-workflow` skill using the Skill tool.

If the user provided a PR number as an argument, pass it in your invocation context. Otherwise, the skill will auto-detect the merged PR for the current branch.

The skill will:
1. Verify the PR was actually merged
2. Identify all issues referenced in the PR
3. Switch to main branch and pull latest changes (MANDATORY)
4. Delete the merged feature branch - local and remote (MANDATORY)
5. Add confirmation comments to EVERY linked issue (MANDATORY)
6. Verify issues are properly closed (MANDATORY)
7. Check CI/CD build status on main branch (MANDATORY)
8. Provide a completion summary report (MANDATORY)

Execute the skill now.
