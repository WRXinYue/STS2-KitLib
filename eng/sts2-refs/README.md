# Pinned STS2 compile refs (Git LFS)

Public (stable) and Steam beta currently ship the same **v0.107.1** API from the same install path. KitLib keeps **separate ref trees** so beta can diverge later without rewiring the repo.

```
eng/sts2-refs/
  stable/0.107.1/data_sts2_windows_x86_64/sts2.dll
  stable/0.107.1/data_sts2_windows_x86_64/0Harmony.dll
  beta/0.107.1/data_sts2_windows_x86_64/sts2.dll
  beta/0.107.1/data_sts2_windows_x86_64/0Harmony.dll
```

Versions match `Sts2ProfileMap` / `scripts/lib/sts2_profiles.py`.

**Dropped:** pre-0.106 API line (`0.103.3` refs removed).

## Refresh refs

When Steam beta moves ahead of public, switch branch and capture only beta:

```bash
make capture-sts2-ref PROFILE=beta
```

When public catches up or you refresh both from the same build:

```bash
make capture-sts2-ref PROFILE=stable
make capture-sts2-ref PROFILE=beta
```

Then `git add eng/sts2-refs` and commit (LFS tracks `*.dll`).
