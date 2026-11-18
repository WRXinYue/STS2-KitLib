---
name: fix-github-issue
description: Investigate and fix a specified GitHub issue in KitLib when the user requests the issue-to-pull-request workflow.
---

Run repository commands from the repository root. Resolve repository file paths relative to that root.

Analyze and fix the GitHub issue identified by the URL or issue number supplied in the user's request.

Follow these steps:

1. Use `gh issue view` to get the issue details
2. Understand the problem described in the issue
3. Search the codebase for relevant files
4. Implement the necessary changes to fix the issue
5. Write and run tests to verify the fix
6. Ensure code passes linting and type checking
7. Create a descriptive commit message
8. Push and create a PR

Remember to use the GitHub CLI (`gh`) for all GitHub-related tasks.
