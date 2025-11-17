---
description: Use master-code-reviewer agent to review code and post feedback to PR thread
---

# INSTRUCTION FOR CLAUDE

Invoke the `code-review-workflow` skill using the Skill tool.

If the user provided a PR number as an argument, pass it in your invocation context. Otherwise, the skill will auto-detect the PR for the current branch.

The skill will:
1. Detect active pull requests for the current branch
2. Orchestrate the master-code-reviewer agent for comprehensive analysis
3. Automatically post review feedback to PR threads (MANDATORY when PR exists)
4. Provide severity-rated findings and actionable recommendations
5. Pause for user review after completion

Execute the skill now.
