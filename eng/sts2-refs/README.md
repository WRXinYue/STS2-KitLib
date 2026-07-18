# Pinned STS2 compile refs (Git LFS)

KitLib compiles against the **public-beta** STS2 build pinned under `beta/<version>/`.

## Refresh refs

When Steam beta moves ahead, switch to the public-beta branch and capture:

```bash
make capture-sts2-ref
```

Then `git add eng/sts2-refs` and commit (LFS tracks `*.dll`).
