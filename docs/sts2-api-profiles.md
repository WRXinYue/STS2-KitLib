# STS2 dual-profile API checks

KitLib supports two pinned game API lines (see `Sts2ProfileMap`):

| Profile | Pinned version | MSBuild |
|---------|----------------|---------|
| stable | 0.103.3 | `-p:Sts2Profile=stable` |
| beta | 0.107.0 | `-p:Sts2Profile=beta` |

Dual-profile builds use **Git LFS refs** under [`eng/sts2-refs/`](../eng/sts2-refs/) — not two Steam installs.

## One-time setup

```bash
git lfs install
```

## Refresh refs (single Steam install)

Steam only keeps one branch at a time. Capture each line after switching:

```bash
# Steam → stable/public branch
make capture-sts2-ref PROFILE=stable

# Steam → beta branch
make capture-sts2-ref PROFILE=beta

git add eng/sts2-refs .gitattributes
git commit -m "Update STS2 compile refs"
```

Refs are stored as:

```
eng/sts2-refs/stable/0.103.3/data_sts2_windows_x86_64/sts2.dll
eng/sts2-refs/beta/0.107.0/data_sts2_windows_x86_64/sts2.dll
```

(plus `0Harmony.dll` in the same folder)

Profile builds (`make build-profiles`) compile against these refs and skip ILRepack merge; use `make sync` with `local.props` Sts2Dir for a deployable mod bundle.

## Daily dev

`make init` writes `local.props` (`Sts2Dir`, `Sts2Profile`) → `make sync-full` deploys to that install. Re-run `make init` after switching Steam branch. Profile CI gates use LFS refs.

## Commands

| Target | Purpose |
|--------|---------|
| `make capture-sts2-ref PROFILE=stable\|beta` | Copy DLLs from `local.props` Sts2Dir into `eng/sts2-refs/` |
| `make build-stable` | Compile against stable ref |
| `make build-beta` | Compile against beta ref |
| `make build-profiles` | Both builds |
| `make check-api` | Reflect touchpoints against both refs |
| `make verify-profiles` | Pre-release gate |

## CI

GitHub Actions checks out with `lfs: true` and runs `make build-profiles` + `make check-api` when refs are present.

## When Megacrit ships a new line

1. Bump pinned versions in `Sts2ProfileMap` and `scripts/lib/sts2_profiles.py`.
2. Re-capture both refs.
3. `make verify-profiles` → fix code / `eng/api_touchpoints.yaml` aliases → CHANGELOG.
