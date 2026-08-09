---
title:
  en: Mod settings pages
  zh-CN: Mod 设置页
cover: https://wrxinyue.s3.bitiful.net/slay-the-spire-2-wallpaper.webp
---

## Overview{lang="en"}

## 概述{lang="zh-CN"}

::: en
Two layers for **KitLib-native** mod settings (shown in **Main menu → Mods → your mod** when **KitModPanel** is installed):

| Layer | API | Role |
| --- | --- | --- |
| **Pages** | `KitLibModSettingsRegistry` | Register / read / unregister pages |
| **Controls** | `KitLib.Modding.ModSettingsUi` | Build toggles, sliders, choices, … inside `BuildBody` |

| | |
| --- | --- |
| **Page types** | `KitLib.Abstractions.Modding` |
| **Control bridge** | `KitLib.Modding.ModSettingsUi` (`KitLib.dll` / Core) |
| **Wired by** | KitModPanel (`KitLibModSettingsUiApi`) |
| **Host probe** | `KitLibHost.TryGetModSettingsPanelHost()` |

**Compile:** `STS2.KitLib.Abstractions` + `KitLib.dll` (for `ModSettingsUi`) + Godot / STS2.
**Runtime:** KitModPanel must be loaded (`ModSettingsUi.IsAvailable`).
:::

::: zh-CN
**KitLib 原生** mod 设置分两层（安装 **KitModPanel** 后在 **主菜单 → Mods → 你的 mod** 显示）：

| 层 | API | 作用 |
| --- | --- | --- |
| **页面** | `KitLibModSettingsRegistry` | 注册 / 读取 / 注销设置页 |
| **控件** | `KitLib.Modding.ModSettingsUi` | 在 `BuildBody` 里拼开关、滑条、下拉等 |

| | |
| --- | --- |
| **页面类型** | `KitLib.Abstractions.Modding` |
| **控件桥接** | `KitLib.Modding.ModSettingsUi`（`KitLib.dll` / Core） |
| **接线方** | KitModPanel（`KitLibModSettingsUiApi`） |
| **宿主探测** | `KitLibHost.TryGetModSettingsPanelHost()` |

**编译：** `STS2.KitLib.Abstractions` + `KitLib.dll`（`ModSettingsUi`）+ Godot / STS2。
**运行时：** 必须已加载 KitModPanel（`ModSettingsUi.IsAvailable`）。
:::

## Native vs Ritsu{lang="en"}

## 原生 vs Ritsu{lang="zh-CN"}

::: en
| Path | When to use |
| --- | --- |
| **KitLib-native** (this page) | You own the UI; no RitsuLib settings framework |
| **STS2-RitsuLib** `ModSettingsRegistry` | Structured entries / bindings from Ritsu |

**Precedence:** if the **same mod id** also registered Ritsu settings pages, KitModPanel shows the **Ritsu** surface and **ignores** KitLib-native pages for that mod. Do not mix both for one mod unless you intend Ritsu to win.
:::

::: zh-CN
| 路径 | 何时用 |
| --- | --- |
| **KitLib 原生**（本文） | 自己做 UI；不依赖 RitsuLib 设置框架 |
| **STS2-RitsuLib** `ModSettingsRegistry` | 用 Ritsu 的结构化 Entry / 绑定 |

**优先规则：** 同一 **mod id** 若也注册了 Ritsu 设置页，KitModPanel 只显示 **Ritsu**，**忽略**该 mod 的 KitLib 原生页。不要混用，除非你就是要让 Ritsu 胜出。
:::

## Convention{lang="en"}

## 规范{lang="zh-CN"}

::: en
1. **`ModId`** — official mod manifest id (case-insensitive).
2. **`PageId`** — stable within the mod; re-register replaces the same pair.
3. **`Title`** / optional **`TitleKey`** — fallback + loc key (`I18N.T` at refresh).
4. **`SortOrder`** — lower first; ties by `PageId`.
5. **`BuildBody`** — must return a Godot `Control`. Prefer `ModSettingsUi` builders for chrome-consistent rows.
6. Register at mod init; unregister on teardown if needed.
:::

::: zh-CN
1. **`ModId`** — 官方清单 id（大小写不敏感）。
2. **`PageId`** — mod 内稳定；同对再次注册会替换。
3. **`Title`** / 可选 **`TitleKey`** — 回退标题 + 本地化 key（刷新时 `I18N.T`）。
4. **`SortOrder`** — 越小越前；相同按 `PageId`。
5. **`BuildBody`** — 必须返回 Godot `Control`。样式一致的行优先用 `ModSettingsUi`。
6. 在 mod 初始化时注册；需要时注销。
:::

## Basic usage{lang="en"}

## 基本用法{lang="zh-CN"}

::: en
```csharp
using Godot;
using KitLib.Abstractions.Modding;
using KitLib.Modding;
using MegaCrit.Sts2.Core.Modding;

[ModInitializer]
public static class Main {
    static bool _enableFeature = true;
    static int _difficulty = 1;

    public static void Initialize() {
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = "MyMod",
            PageId = "general",
            Title = "General",
            TitleKey = "my_mod.settings.general",
            SortOrder = 0,
            BuildBody = BuildGeneralPage,
        });
    }

    static Control BuildGeneralPage() {
        if (!ModSettingsUi.IsAvailable)
            return new Label { Text = "KitModPanel is required for styled settings." };

        var root = ModSettingsUi.CreatePageStack();
        root.AddChild(ModSettingsUi.CreateSectionHeader("Gameplay", "Core options"));
        root.AddChild(ModSettingsUi.CreateBoolToggle(
            "Enable feature",
            "Turns the main feature on or off.",
            () => _enableFeature,
            v => _enableFeature = v));
        root.AddChild(ModSettingsUi.CreateChoiceRow(
            "Difficulty",
            null,
            [
                new KitLibModSettingsChoice("Easy", 0),
                new KitLibModSettingsChoice("Normal", 1),
                new KitLibModSettingsChoice("Hard", 2),
            ],
            () => _difficulty,
            v => _difficulty = v));
        return root;
    }
}
```
:::

::: zh-CN
```csharp
using Godot;
using KitLib.Abstractions.Modding;
using KitLib.Modding;
using MegaCrit.Sts2.Core.Modding;

[ModInitializer]
public static class Main {
    static bool _enableFeature = true;
    static int _difficulty = 1;

    public static void Initialize() {
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = "MyMod",
            PageId = "general",
            Title = "General",
            TitleKey = "my_mod.settings.general",
            SortOrder = 0,
            BuildBody = BuildGeneralPage,
        });
    }

    static Control BuildGeneralPage() {
        if (!ModSettingsUi.IsAvailable)
            return new Label { Text = "需要 KitModPanel 才能使用样式化设置行。" };

        var root = ModSettingsUi.CreatePageStack();
        root.AddChild(ModSettingsUi.CreateSectionHeader("玩法", "核心选项"));
        root.AddChild(ModSettingsUi.CreateBoolToggle(
            "启用功能",
            "开关主要功能。",
            () => _enableFeature,
            v => _enableFeature = v));
        root.AddChild(ModSettingsUi.CreateChoiceRow(
            "难度",
            null,
            [
                new KitLibModSettingsChoice("简单", 0),
                new KitLibModSettingsChoice("普通", 1),
                new KitLibModSettingsChoice("困难", 2),
            ],
            () => _difficulty,
            v => _difficulty = v));
        return root;
    }
}
```
:::

## Form builders{lang="en"}

## 表单控件{lang="zh-CN"}

::: en
`KitLib.Modding.ModSettingsUi` — throws if KitModPanel did not wire builders. Check `IsAvailable` first.

| Member | Purpose |
| --- | --- |
| `CreatePageStack()` | Vertical stack with KitLib spacing |
| `CreateSectionHeader(title, description?)` | Section title + optional blurb |
| `CreateBoolToggle(title, description?, get, set)` | Checkbox row |
| `CreateChoiceRow(title, description?, options, getId, setId)` | Dropdown (`KitLibModSettingsChoice`) |
| `CreateIntSlider(title, description?, get, set, min, max, step?)` | Integer slider |
| `CreateFloatSlider(title, description?, get, set, min, max, step?)` | Float slider |
| `CreateStringField(title, description?, get, set, multiline?)` | Line or multiline text |
| `CreateColorRow(title, description?, get, set)` | Color picker (`KitLibModSettingsRgb`) |
| `CreateActionButton(title, description?, onPressed)` | Accent action button |
| `RefreshBoolToggles()` | Sync bool rows after external writes |

### Examples

**Sliders**

```csharp
root.AddChild(ModSettingsUi.CreateIntSlider(
    "Max allies", null, () => _maxAllies, v => _maxAllies = v, 1, 8));
root.AddChild(ModSettingsUi.CreateFloatSlider(
    "Volume", null, () => _volume, v => _volume = v, 0f, 1f, 0.05f));
```

**String + color + button**

```csharp
root.AddChild(ModSettingsUi.CreateStringField(
    "Display name", null, () => _name, v => _name = v));
root.AddChild(ModSettingsUi.CreateColorRow(
    "Tint",
    null,
    () => _tint,
    v => _tint = v));
root.AddChild(ModSettingsUi.CreateActionButton(
    "Reset",
    "Restore defaults",
    ResetDefaults));
```
:::

::: zh-CN
`KitLib.Modding.ModSettingsUi` — 若 KitModPanel 未接线会抛错；先检查 `IsAvailable`。

| 成员 | 作用 |
| --- | --- |
| `CreatePageStack()` | 带 KitLib 间距的纵向容器 |
| `CreateSectionHeader(title, description?)` | 分区标题 + 可选说明 |
| `CreateBoolToggle(title, description?, get, set)` | 开关行 |
| `CreateChoiceRow(title, description?, options, getId, setId)` | 下拉（`KitLibModSettingsChoice`） |
| `CreateIntSlider(...)` | 整数滑条 |
| `CreateFloatSlider(...)` | 浮点滑条 |
| `CreateStringField(..., multiline?)` | 单行 / 多行文本 |
| `CreateColorRow(...)` | 取色（`KitLibModSettingsRgb`） |
| `CreateActionButton(...)` | 强调操作按钮 |
| `RefreshBoolToggles()` | 外部改值后同步开关行 |

### 示例

**滑条**

```csharp
root.AddChild(ModSettingsUi.CreateIntSlider(
    "最大盟友", null, () => _maxAllies, v => _maxAllies = v, 1, 8));
root.AddChild(ModSettingsUi.CreateFloatSlider(
    "音量", null, () => _volume, v => _volume = v, 0f, 1f, 0.05f));
```

**文本 + 颜色 + 按钮**

```csharp
root.AddChild(ModSettingsUi.CreateStringField(
    "显示名", null, () => _name, v => _name = v));
root.AddChild(ModSettingsUi.CreateColorRow(
    "色调",
    null,
    () => _tint,
    v => _tint = v));
root.AddChild(ModSettingsUi.CreateActionButton(
    "重置",
    "恢复默认",
    ResetDefaults));
```
:::

## Register / read / unregister{lang="en"}

## 注册 / 读取 / 注销{lang="zh-CN"}

::: en
```csharp
void Register(KitLibModSettingsPageRegistration page)
bool Unregister(string modId, string pageId)
int UnregisterAll(string modId)
bool HasPages(string modId)
bool Contains(string modId, string pageId)
bool TryGetPage(string modId, string pageId, out KitLibModSettingsPageRegistration? page)
IReadOnlyList<KitLibModSettingsPageRegistration> GetPages(string modId)
IReadOnlyList<string> GetRegisteredModIds()
string ResolveTitle(KitLibModSettingsPageRegistration page, Func<string, string, string>? translate = null)
```

### `KitLibModSettingsPageRegistration`

| Member | Type | Required | Description |
| --- | --- | --- | --- |
| `ModId` | `string` | yes | Manifest id |
| `PageId` | `string` | yes | Stable page id |
| `Title` | `string` | yes | Fallback label |
| `TitleKey` | `string?` | no | Loc key |
| `SortOrder` | `int` | no (`0`) | Tab order |
| `BuildBody` | `Func<object>` | yes | Returns Godot `Control` |

### Host availability

```csharp
using KitLib.Host;

var host = KitLibHost.TryGetModSettingsPanelHost();
if (host is { IsModuleLoaded: true }) {
    // KitModPanel is up
    // host.IsRitsuBridgeAvailable — STS2-RitsuLib bridge ready
}
```
:::

::: zh-CN
```csharp
void Register(KitLibModSettingsPageRegistration page)
bool Unregister(string modId, string pageId)
int UnregisterAll(string modId)
bool HasPages(string modId)
bool Contains(string modId, string pageId)
bool TryGetPage(string modId, string pageId, out KitLibModSettingsPageRegistration? page)
IReadOnlyList<KitLibModSettingsPageRegistration> GetPages(string modId)
IReadOnlyList<string> GetRegisteredModIds()
string ResolveTitle(KitLibModSettingsPageRegistration page, Func<string, string, string>? translate = null)
```

### `KitLibModSettingsPageRegistration`

| 成员 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `ModId` | `string` | 是 | 清单 id |
| `PageId` | `string` | 是 | 稳定页面 id |
| `Title` | `string` | 是 | 回退标题 |
| `TitleKey` | `string?` | 否 | 本地化 key |
| `SortOrder` | `int` | 否（`0`） | 标签顺序 |
| `BuildBody` | `Func<object>` | 是 | 返回 Godot `Control` |

### 宿主是否可用

```csharp
using KitLib.Host;

var host = KitLibHost.TryGetModSettingsPanelHost();
if (host is { IsModuleLoaded: true }) {
    // KitModPanel 已就绪
    // host.IsRitsuBridgeAvailable — STS2-RitsuLib 桥接可用
}
```
:::

## See also{lang="en"}

## 相关{lang="zh-CN"}

::: en
- [Logging](/api/kitlib-log/)
- [Install](/kitlib/install/) — KitModPanel product folder
- [Mod AI integration](/kitai/mod-ai-integration/)
:::

::: zh-CN
- [日志](/api/kitlib-log/)
- [安装](/kitlib/install/) — KitModPanel 产品目录
- [Mod AI 集成](/kitai/mod-ai-integration/)
:::
