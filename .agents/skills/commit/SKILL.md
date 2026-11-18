---
name: commit
description: Commit the relevant KitLib working-tree changes when the user requests a commit, using an optional commit-message hint.
---

Run repository commands from the repository root. Resolve repository file paths relative to that root.

Commit all current changes. Arguments (optional): commit message hint, or leave empty to auto-generate from the diff.

Steps:

1. Run `git status` and `git diff` to understand what has changed.

2. Stage only relevant source files — do NOT stage:
   - `.env` or any credential/secret files
   - Large binaries not already tracked by Git LFS
   - Unrelated untracked files

3. Draft a commit message following Conventional Commits:
   - Prefix: `feat` / `fix` / `refactor` / `chore` / `docs`
   - Keep the description concise and in English
   - Example: `fix: resolve infinite loop on reward screen transition`
   - If the user provides a commit-message hint, use it as the basis for the message

4. Commit (do NOT push):
   ```
   git add <files>
   git commit -m "type: description"
   ```
