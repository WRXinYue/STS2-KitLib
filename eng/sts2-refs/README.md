# Pinned STS2 compile refs (Git LFS)

The supported build and CI baselines are stable **v0.111.1** (commit
`cc1f9462`) and public-beta **v0.113.1** (commit `d09d48e5`).

Snapshots are stored directly under `eng/sts2-refs/<version>/`. The directories
are `0.111.1/` and `0.113.1/`. Build profiles select a version, while the
snapshot paths use only the version number.

The snapshot contains the original managed `sts2.dll`, `0Harmony.dll`, and
`Steamworks.NET.dll`. These assemblies were captured from the macOS arm64
Steam installation. The `data_sts2_windows_x86_64` directory name is the
normalized MSBuild reference layout used on every CI platform.

`release_info.json` records the game's version and commit.
`ref-manifest.json` records the source data-directory name and SHA-256 hashes.

## Refresh refs

1. Update the version in `scripts/lib/sts2_profiles.py`,
   `eng/Sts2Refs.props`, `eng/KitLib.PersonalCompat.props`, and
   `src/KitLib/Abstractions/Compat/Sts2ProfileMap.cs`.
2. Switch Steam to the matching game build and capture its profile, then verify both baselines:

   ```bash
   make capture-sts2-ref PROFILE=stable  # v0.111.1
   # Or PROFILE=beta for v0.113.1
   make extract-touchpoints
   python3 scripts/verify_sts2_refs.py
   make build-profiles
   make check-api
   make check-smoke-mod
   ```

3. Commit the new snapshot, metadata, and configuration together. Git LFS tracks
   all three DLLs. Remove the superseded snapshot when retiring its baseline.

Capture rejects a game whose `release_info.json` differs from the pin.
CI builds and runs smoke tests for both profiles. It verifies configuration
agreement, release identity, PE file headers, and
checksums before compiling. It regenerates the API manifest and checks for drift.

API touchpoint checks cover statically identified members. Dynamic Harmony
targets and optional fallbacks still require in-game validation.
