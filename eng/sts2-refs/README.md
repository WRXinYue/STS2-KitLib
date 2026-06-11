# STS2 compile refs (Git LFS)

Pinned `sts2.dll` and `0Harmony.dll` snapshots for dual-profile builds (`make build-profiles`).

Layout mirrors a game install root:

```
eng/sts2-refs/
  stable/0.103.3/data_sts2_windows_x86_64/sts2.dll
  stable/0.103.3/data_sts2_windows_x86_64/0Harmony.dll
  beta/0.107.0/data_sts2_windows_x86_64/sts2.dll
  beta/0.107.0/data_sts2_windows_x86_64/0Harmony.dll
```

Versions match `Sts2ProfileMap` / `scripts/lib/sts2_profiles.py`.

Only `sts2.dll` and `0Harmony.dll` are stored (Git LFS). That is enough for compile-time API checks; `make build-profiles` sets `KitLibProfileBuild=true` to skip ILRepack (which needs the full game runtime folder). Use `make sync` against a live Steam install for deployable builds.

## Refresh (one Steam install, two branches)

1. Switch Steam to **stable/public** branch.
2. `make capture-sts2-ref PROFILE=stable`
3. Switch Steam to **beta** branch.
4. `make capture-sts2-ref PROFILE=beta`
5. `git add eng/sts2-refs && git commit`

Requires [Git LFS](https://git-lfs.com/) (`git lfs install` once per machine).

`make build-profiles` uses these refs only. Daily `make sync` uses `local.props` Sts2Dir (one Steam install).
