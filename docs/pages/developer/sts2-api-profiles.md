---
title:
  en: STS2 compile refs (maintainers)
  zh-CN: STS2 编译引用（维护者）
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

::: en
Pinned beta API refs, LFS capture, and CI checks for KitLib maintainers. Mod authors who only need runtime compatibility should start with **[STS2 version compatibility](/developer/extending/sts2-compat)**.
:::

::: zh-CN
KitLib 维护者用的 beta API 引用、LFS 捕获与 CI 检查。仅需运行时兼容说明的 mod 作者请先看 **[STS2 版本兼容](/developer/extending/sts2-compat)**。
:::

## Pinned beta ref{lang="en"}

## 固定的 beta 引用{lang="zh-CN"}

::: en
KitLib compiles against one pinned game build (see `scripts/lib/sts2_profiles.py` and `Sts2ProfileMap.PinnedGameVersion`):

| Setting | Value |
| --- | --- |
| Profile | `beta` |
| Pinned version | `0.109.0` (update when Steam beta moves) |
| MSBuild | `-p:Sts2Profile=beta` (default) |
| Ref path | `eng/sts2-refs/beta/0.109.0/data_sts2_windows_x86_64/sts2.dll` |

CI and `dotnet format` use the LFS ref — not your local Steam install.
:::

::: zh-CN
KitLib 只针对一条固定的游戏构建编译（见 `scripts/lib/sts2_profiles.py` 与 `Sts2ProfileMap.PinnedGameVersion`）：

| 项 | 值 |
| --- | --- |
| Profile | `beta` |
| 固定版本 | `0.109.0`（Steam beta 前进时更新） |
| MSBuild | `-p:Sts2Profile=beta`（默认） |
| Ref 路径 | `eng/sts2-refs/beta/0.109.0/data_sts2_windows_x86_64/sts2.dll` |

CI 与 `dotnet format` 使用 LFS ref，而非本机 Steam 安装目录。
:::

## One-time setup{lang="en"}

## 一次性准备{lang="zh-CN"}

```bash
git lfs install
```

## Refresh refs{lang="en"}

## 刷新 ref{lang="zh-CN"}

```bash
# Steam → public-beta branch
make capture-sts2-ref

git add eng/sts2-refs .gitattributes
git commit -m "Update STS2 beta compile ref"
```

Refs are stored as:

```
eng/sts2-refs/beta/0.109.0/data_sts2_windows_x86_64/sts2.dll
eng/sts2-refs/beta/0.109.0/data_sts2_windows_x86_64/0Harmony.dll
```

## Daily dev{lang="en"}

## 日常开发{lang="zh-CN"}

::: en
`make init` writes `local.props` (`Sts2Dir`, `Sts2Profile=beta`) → `make sync-full` builds and deploys to that install. Local builds still compile against `eng/sts2-refs/beta/` when `Sts2Dir` is unset (CI-style).
:::

::: zh-CN
`make init` 写入 `local.props`（`Sts2Dir`、`Sts2Profile=beta`）→ `make sync-full` 构建并部署到该安装。未设置 `Sts2Dir` 时本地构建仍使用 `eng/sts2-refs/beta/`（与 CI 一致）。
:::

## Commands{lang="en"}

## 命令{lang="zh-CN"}

| Target | Purpose |
| --- | --- |
| `make capture-sts2-ref` | Copy DLLs from `local.props` Sts2Dir into `eng/sts2-refs/beta/` |
| `make build` | Compile mod bundle against beta ref |
| `make check-api` | Reflect touchpoints against beta `sts2.dll` |
| `make verify` | `build` + `check-api` (pre-release) |
| `make extract-touchpoints` | Regenerate `eng/api_touchpoints.yaml` from `src/` |

## When Megacrit ships a new beta{lang="en"}

## Steam beta 更新 API 时{lang="zh-CN"}

::: en
1. Bump pinned version in `scripts/lib/sts2_profiles.py`, `eng/Sts2Refs.props`, `eng/api_touchpoints.yaml`, and `Sts2ProfileMap.PinnedGameVersion`.
2. `make capture-sts2-ref` from a matching Steam install.
3. `make verify` → fix code / touchpoint aliases → CHANGELOG.
:::

::: zh-CN
1. 更新 `scripts/lib/sts2_profiles.py`、`eng/Sts2Refs.props`、`eng/api_touchpoints.yaml`、`Sts2ProfileMap.PinnedGameVersion` 中的固定版本。
2. 在匹配的 Steam 安装上执行 `make capture-sts2-ref`。
3. `make verify` → 修代码 / touchpoint alias → CHANGELOG。
:::
