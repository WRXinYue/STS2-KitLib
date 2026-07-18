# Pinned STS2 compile refs (Git LFS)

KitLib keeps **separate ref trees** for stable and beta so each profile can pin a different game build.

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
