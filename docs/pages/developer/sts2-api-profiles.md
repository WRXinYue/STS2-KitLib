---
title:
  en: STS2 API profiles (maintainers)
  zh-CN: STS2 API profiles（维护者）
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

::: en
Dual-profile API refs, LFS capture, and CI checks for KitLib maintainers. Mod authors who only need build conventions should start with **[STS2 version compatibility](/developer/extending/sts2-compat)**.
:::

::: zh-CN
KitLib 维护者用的双 profile API 引用、LFS 捕获与 CI 检查。仅需构建约定的 mod 作者请先看 **[STS2 版本兼容](/developer/extending/sts2-compat)**。
:::

## Dual-profile API checks{lang="en"}

## 双 profile API 检查{lang="zh-CN"}

::: en
KitLib supports two pinned game API lines (see `Sts2ProfileMap`):

| Profile | Pinned version | MSBuild |
| --- | --- | --- |
| stable | 0.103.3 | `-p:Sts2Profile=stable` |
| beta | 0.107.0 | `-p:Sts2Profile=beta` |

Dual-profile builds use **Git LFS refs** under `eng/sts2-refs/` — not two Steam installs.
:::

::: zh-CN
KitLib 支持两条固定的游戏 API 线（见 `Sts2ProfileMap`）：

| Profile | 固定版本 | MSBuild |
| --- | --- | --- |
| stable | 0.103.3 | `-p:Sts2Profile=stable` |
| beta | 0.107.0 | `-p:Sts2Profile=beta` |

双 profile 构建使用 **`eng/sts2-refs/` 下的 Git LFS ref**，不要求两个 Steam 安装并存。
:::

## One-time setup{lang="en"}

## 一次性准备{lang="zh-CN"}

```bash
git lfs install
```

## Refresh refs (single Steam install){lang="en"}

## 刷新 ref（单个 Steam 安装）{lang="zh-CN"}

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

## Daily dev{lang="en"}

## 日常开发{lang="zh-CN"}

::: en
`make init` writes `local.props` (`Sts2Dir`, optional `Sts2Profile`) → `make sync-full` deploys to that install. **`make build` / `make sync-full` auto-detect profile** from `release_info.json` or a hash match against `eng/sts2-refs/` when `Sts2Profile` is omitted. Set `<Sts2Profile>` in `local.props` or `STS2_PROFILE` in `.env` to override. Re-run `make init` after switching Steam branch if you want the override written for you. Profile CI gates use LFS refs.
:::

::: zh-CN
`make init` 写入 `local.props`（`Sts2Dir`、可选 `Sts2Profile`）→ `make sync-full` 部署到该安装。**省略 `Sts2Profile` 时 `make build` / `make sync-full` 会根据 `release_info.json` 或与 `eng/sts2-refs/` 的哈希匹配自动检测 profile**。在 `local.props` 或 `.env` 的 `STS2_PROFILE` 中可强制指定。切换 Steam 分支后若希望写入覆盖值，可重新 `make init`。Profile CI 门禁使用 LFS ref。
:::

## Commands{lang="en"}

## 命令{lang="zh-CN"}

| Target | Purpose |
| --- | --- |
| `make capture-sts2-ref PROFILE=stable\|beta` | Copy DLLs from `local.props` Sts2Dir into `eng/sts2-refs/` |
| `make build-stable` | Compile against stable ref |
| `make build-beta` | Compile against beta ref |
| `make build-profiles` | Both builds |
| `make check-api` | Reflect touchpoints against both refs |
| `make verify-profiles` | Pre-release gate |

## When Megacrit ships a new line{lang="en"}

## Megacrit 发布新 API 线时{lang="zh-CN"}

::: en
1. Bump pinned versions in `Sts2ProfileMap` and `scripts/lib/sts2_profiles.py`.
2. Re-capture both refs.
3. `make verify-profiles` → fix code / `eng/api_touchpoints.yaml` aliases → CHANGELOG.
:::

::: zh-CN
1. 更新 `Sts2ProfileMap` 与 `scripts/lib/sts2_profiles.py` 中的固定版本。
2. 重新 capture 两侧 ref。
3. `make verify-profiles` → 修代码 / `eng/api_touchpoints.yaml` alias → CHANGELOG。
:::
