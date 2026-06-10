#!/usr/bin/env python3
"""
Pre-build icon tree-shaker for DevMode.
Scans src/**/*.cs for MdiIcon usages, extracts icons from icons/mdi/icons.json,
writes per-module Icons/mdi-used.json and MdiIcon.Generated.cs (Panel + ModPanel).

Run:  python scripts/shake_icons.py
MSBuild: KitLib.Core.csproj ShakeIcons target (python scripts/shake_icons.py).
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import OrderedDict
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.ensure_iconify_mdi import ensure_mdi_icons  # noqa: E402

SKIP_NAMES = frozenset({"Name", "IsAvailable", "Texture"})

# Exclude MdiIcon.Get(…) / MdiIcon.From(…) — otherwise [A-Za-z]+ backtracks to "Ge"/"Fro".
USAGE_RE = re.compile(r"(?<![\w.])MdiIcon\.(?!(?:Get|From)\s*\()([A-Z][A-Za-z0-9]+)(?!\s*\()")
DEF_RE = re.compile(r"(?:public\s+)?static\s+readonly\s+MdiIcon\s+([A-Z][A-Za-z0-9]+)\s*=\s*new\(\"([^\"]+)\"\)")
GET_RE = re.compile(r"MdiIcon\.Get\(\s*\"([^\"]+)\"")
FROM_RE = re.compile(r"MdiIcon\.From\(\s*\"([^\"]+)\"\s*\)")
ICON_KEY_RE = re.compile(r"IconKey\s*=\s*\"([^\"]+)\"")
REGISTER_TAB_ICON_RE = re.compile(r"Register(?:Action)?Tab\(\s*\"[^\"]+\"\s*,\s*\"([^\"]+)\"")


def _is_generated_icon_cs(path: Path) -> bool:
    return path.name == "MdiIcon.Generated.cs"


def _comment_line(trimmed: str) -> bool:
    return trimmed.startswith("//") or trimmed.startswith("///") or trimmed.startswith("*") or trimmed.startswith("/*")


def kebab_to_pascal(kebab: str) -> str:
    parts = kebab.split("-")
    return "".join(p[:1].upper() + p[1:] if p else "" for p in parts)


def pascal_to_kebab(pascal: str) -> str:
    parts: list[str] = []
    for i, c in enumerate(pascal):
        if c.isupper():
            if i > 0:
                parts.append("-")
            parts.append(c.lower())
        else:
            parts.append(c)
    return "".join(parts)


def scan_sources(src_dir: Path) -> tuple[set[str], dict[str, str]]:
    used_pascal: set[str] = set()
    defined_icons: dict[str, str] = {}

    for cs in sorted(src_dir.rglob("*.cs")):
        if _is_generated_icon_cs(cs):
            continue
        text = cs.read_text(encoding="utf-8")
        for line in text.splitlines():
            dm = DEF_RE.search(line)
            if dm:
                defined_icons[dm.group(1)] = dm.group(2)

            trimmed = line.lstrip()
            if _comment_line(trimmed):
                continue
            if re.match(r"(?:public\s+)?static\s+readonly\s+MdiIcon", trimmed):
                continue

            for m in USAGE_RE.finditer(line):
                name = m.group(1)
                if name in SKIP_NAMES:
                    continue
                used_pascal.add(name)

    return used_pascal, defined_icons


def scan_get_from(src_dir: Path) -> tuple[dict[str, str], dict[str, str]]:
    """Returns (get_kebabs, from_kebabs) as kebab -> label for logging."""
    get_map: dict[str, str] = {}
    from_map: dict[str, str] = {}

    for cs in sorted(src_dir.rglob("*.cs")):
        if _is_generated_icon_cs(cs):
            continue
        for line in cs.read_text(encoding="utf-8").splitlines():
            trimmed = line.lstrip()
            if _comment_line(trimmed):
                continue
            for m in GET_RE.finditer(line):
                k = m.group(1)
                get_map.setdefault(k, f'Get("{k}")')
            for m in FROM_RE.finditer(line):
                k = m.group(1)
                from_map.setdefault(k, f'From("{k}")')
    return get_map, from_map


def scan_rail_tab_icon_keys(src_dir: Path) -> dict[str, str]:
    """Collect kebab icon ids from satellite *TabRegistration.cs (KitLibHost.RegisterTab)."""
    keys: dict[str, str] = {}

    for cs in sorted(src_dir.rglob("*TabRegistration.cs")):
        for line in cs.read_text(encoding="utf-8").splitlines():
            trimmed = line.lstrip()
            if _comment_line(trimmed):
                continue
            for m in ICON_KEY_RE.finditer(line):
                k = m.group(1)
                keys.setdefault(k, kebab_to_pascal(k))
            for m in REGISTER_TAB_ICON_RE.finditer(line):
                k = m.group(1)
                keys.setdefault(k, kebab_to_pascal(k))
    return keys


_ICON_BUNDLES = (
    "KitLib.Modules.ModPanel",
    "KitLib.Modules.Panel",
)


def _shake_bundle(repo_root: Path, full_json: Path, module_name: str) -> int:
    module_dir = repo_root / "src" / module_name
    if not module_dir.is_dir():
        return 0

    icons_dir = module_dir / "Icons"
    out_json = icons_dir / "mdi-used.json"
    out_gen = icons_dir / "MdiIcon.Generated.cs"

    used_pascal, defined_icons = scan_sources(module_dir)

    kebab_map: dict[str, str] = {}
    for p in used_pascal:
        kebab = defined_icons[p] if p in defined_icons else pascal_to_kebab(p)
        kebab_map[kebab] = p

    get_labels, from_labels = scan_get_from(module_dir)
    for kebab, label in get_labels.items():
        kebab_map.setdefault(kebab, label)
    for kebab, label in from_labels.items():
        kebab_map.setdefault(kebab, label)

    tab_scan_root = repo_root / "src" if module_name == "KitLib.Modules.Panel" else module_dir
    tab_icon_labels = scan_rail_tab_icon_keys(tab_scan_root)
    for kebab, label in tab_icon_labels.items():
        kebab_map.setdefault(kebab, label)

    if not kebab_map and defined_icons:
        print(f"[{module_name}] No explicit usages found - bundling all {len(defined_icons)} defined icons")
        for pascal, kebab in defined_icons.items():
            kebab_map[kebab] = pascal

    if not kebab_map:
        print(f"[{module_name}] No icon usages — skipped.")
        return 0

    print(f"[{module_name}] Found {len(kebab_map)} icon(s) to bundle")
    for k in sorted(kebab_map):
        print(f"  MdiIcon.{kebab_map[k]}  ->  mdi:{k}")

    full_data = json.loads(full_json.read_text(encoding="utf-8"))
    prefix = full_data.get("prefix", "")
    view_box = full_data.get("height", 24)
    icons_full = full_data.get("icons") or {}

    extracted: OrderedDict[str, dict] = OrderedDict()
    missing: list[str] = []

    for kebab in sorted(kebab_map):
        if kebab not in icons_full:
            missing.append(f"  MdiIcon.{kebab_map[kebab]} -> mdi:{kebab} (NOT FOUND)")
            continue
        icon = icons_full[kebab]
        entry: dict = {"body": icon["body"]}
        if "width" in icon:
            entry["width"] = icon["width"]
        if "height" in icon:
            entry["height"] = icon["height"]
        extracted[kebab] = entry

    if missing:
        msg = (
            f"shake_icons [{module_name}]: {len(missing)} icon(s) referenced in code but missing from icons.json. "
            f"Add the icon set or fix the name:\n" + "\n".join(missing)
        )
        print(msg, file=sys.stderr)
        return 1

    output = OrderedDict([("prefix", prefix), ("viewBox", view_box), ("icons", extracted)])
    icons_dir.mkdir(parents=True, exist_ok=True)
    out_json.write_text(json.dumps(output, indent=2) + "\n", encoding="utf-8")

    total_icons = len(icons_full)
    print(f"\n[{module_name}] Wrote {len(extracted)} icon(s) to {out_json}")
    print(f"[{module_name}] Full set: {total_icons} icons -> trimmed to {len(extracted)}")

    ns = "KitLib.ModPanel.Icons" if "ModPanel" in module_name else "KitLib.Icons"
    struct_access = "internal" if "ModPanel" in module_name else "public"
    gen_lines = [
        "// <auto-generated> by scripts/shake_icons.py - do not edit.",
        f"namespace {ns};",
        "",
        f"{struct_access} readonly partial struct MdiIcon",
        "{",
    ]
    emitted = 0
    for kebab, label in sorted(kebab_map.items(), key=lambda kv: kv[1]):
        if label.startswith("Get(") or label.startswith("From("):
            continue
        if label in defined_icons:
            continue
        gen_lines.append(f'    public static readonly MdiIcon {label} = new("{kebab}");')
        emitted += 1
    gen_lines.append("}")
    out_gen.write_text("\n".join(gen_lines) + "\n", encoding="utf-8")
    print(f"[{module_name}] Wrote MdiIcon.Generated.cs ({emitted} fields)")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description="Tree-shake MDI icons for DevMode.")
    ap.add_argument(
        "--repo-root",
        type=Path,
        default=_SCRIPT_DIR.parent,
        help="Repository root (default: parent of scripts/)",
    )
    ap.add_argument("--full-json", type=Path, default=None, help="Path to icons/mdi/icons.json")
    args = ap.parse_args()

    repo_root: Path = args.repo_root.resolve()
    full_json = args.full_json or (repo_root / "icons" / "mdi" / "icons.json")

    if not full_json.is_file():
        ensure_mdi_icons(repo_root)
    if not full_json.is_file():
        print(f"icons.json still missing at {full_json} after Ensure-IconifyMdi.", file=sys.stderr)
        return 1

    for module_name in _ICON_BUNDLES:
        code = _shake_bundle(repo_root, full_json, module_name)
        if code != 0:
            return code
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
