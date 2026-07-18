## Summary

<!-- What changed and why. PR title: Conventional Commits (`feat:`, `fix:`, …). -->

## Related issue

<!-- If any: Closes #… / Related to #… -->

## Scope

- [ ] KitLib mod / modules (`src/`)
- [ ] Tools (`tools/KitLib.Mcp`)
- [ ] Repo tooling only (scripts, CI, docs)

## Checklist

- [ ] `dotnet build KitLib.sln` or `make build-all` locally (requires game `sts2.dll` via `make init`)
- [ ] `make check` (`format-check` + `lint-scripts`)
- [ ] Changelog: `CHANGELOG.md` + `CHANGELOG.zh-CN.md` when user-facing
- [ ] Localization: `eng.json` + `zhs.json` updated together when player-facing strings change
- [ ] No `.env`, `local.props`, `build/`, or other ignored artifacts in the diff

## Test plan

<!-- How you verified the change (in-game steps, dev viewer, etc.). -->
