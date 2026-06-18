# KitLib modular architecture

KitLib ships as **one game mod** (`mods/KitLib/`) with a Core DLL and optional satellite DLLs under `modules/`. Each satellite is a **separate compile target**; deleting a module DLL disables that feature at runtime.

## Repository layout (enterprise)

```text
KitLib.sln
KitLib.json                    # Core mod manifest (runtime: mod_manifest.json)
eng/                           # MSBuild props/targets (build infra)
scripts/
manual/                        # User module embedded docs (repo root)
src/
  KitLib.Abstractions/         # NuGet contracts
  KitLib.Core/                 # KitLib.dll — Host, Settings, Harmony entry
  KitLib.Modules.User/         # KitLib.User.dll
  KitLib.Modules.ModPanel/     # KitLib.ModPanel.dll (main-menu mod settings)
  KitLib.Modules.AI/           # KitLib.AI.dll
  KitLib.Modules.Panel/        # KitLib.Panel.dll (+ Icons, Godot assets)
  KitLib.Modules.Cheat/        # KitLib.Cheat.dll
  KitLib.Modules.Dev/          # KitLib.Dev.dll
```

Each project folder contains **its own source tree**. Opening `src/KitLib.Modules.AI/` in Solution Explorer matches the files compiled into `KitLib.AI.dll`.

## Runtime layout

```text
mods/KitLib/
  mod_manifest.json
  KitLib.dll
  KitLib.Abstractions.dll
  modules/
    KitLib.User.dll
    KitLib.ModPanel.dll
    KitLib.AI.dll
    KitLib.Panel.dll
    KitLib.Cheat.dll
    KitLib.Dev.dll
```

## Dependency rules

| Assembly | References | Harmony |
|----------|------------|---------|
| `KitLib.Abstractions` | (none) | — |
| `KitLib` (Core) | Abstractions, game | `MultiplayerCompatPatch` |
| Satellites | Core + Abstractions (+ peers at compile time) | `KitLibHarmony.Apply(assembly, id)` in `ModuleEntry` |

Cross-module internals use `InternalsVisibleTo` within the KitLib family and `KitLib*Ops` delegates on `KitLib.Host` where compile-time cycles must be avoided.

## Build

- **Core**: `src/KitLib.Core/KitLib.Core.csproj` → `build/KitLib/KitLib.dll`
- **Satellites**: `src/KitLib.Modules.*/` → `build/KitLib.*.dll`
- Compile globs: `eng/KitLib.Core.Compile.props`, `eng/KitLib.Satellite.Compile.props` (per-project `**/*.cs`)

```bash
make sync-full    # build-all + deploy bundle to game mods/KitLib/
make zip-full     # package build/KitLib-vX.X.X.zip
```

## Runtime load order

`SatelliteModuleLoader` loads from `mods/KitLib/modules/` according to user settings in `settings.json` (Mod settings → **Modules**; restart required):

1. User → 2. AI → 3. ModPanel → 4. Panel → 5. Cheat (needs Panel) → 6. Dev (needs Panel)

`KitLib.User` and `KitLib.ModPanel` are always loaded. New installs default to the **Standard** profile (Panel on; AI/Cheat/Dev off). Existing settings migrate to **Full** (all modules enabled).

## Content-mod authors

NuGet **`STS2.KitLib.Abstractions`** for compile-time contracts. Runtime needs `KitLib.dll` and any satellite DLLs you depend on under `modules/`.
