Bump the version to $ARGUMENTS and release.

Steps:

1. Update version files:
   - `KitLib.json` → `"version"` field
   - `CHANGELOG.md` → add new `## [VERSION] - DATE` section below `## [Unreleased]`
   - `CHANGELOG.zh-CN.md` → same in Chinese

2. CHANGELOG rules:
   - Only include player-facing changes (features, bug fixes, balance). No refactors or docs.
   - Look at git log since the last version tag to find what changed:
     ```
     git log $(git describe --tags --abbrev=0)..HEAD --oneline
     ```
   - Write English entries in `CHANGELOG.md` and Chinese entries in `CHANGELOG.zh-CN.md`.
   - If there are no player-facing changes, write `- Minor internal improvements.` (EN) / `- 内部小幅优化。` (ZH) as a placeholder.

3. Commit with message: `chore: bump version to VERSION`

4. Tag and push:
   - **Tags must be created on the `main` branch.** Ensure the current branch is `main` before tagging.
   ```
   git tag -a vVERSION -m "vVERSION"
   git push origin main
   git push origin vVERSION
   ```

5. **Stop here.** Do NOT run `make publish` or trigger any GitHub Release automatically.
   - Publishing is a manual step performed by the user.
   - After tagging, tell the user the tag has been pushed and they can publish whenever ready:
     - `make upload-github VERSION=VERSION` — GitHub Release only
     - `make upload-all VERSION=VERSION` — GitHub + Nexus + NuGet (one zip build)
