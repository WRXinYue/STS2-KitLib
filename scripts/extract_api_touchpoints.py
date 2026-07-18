#!/usr/bin/env python3
"""Scan KitLib src/ for game API touchpoints and merge into eng/api_touchpoints.yaml."""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
_REPO_ROOT = _SCRIPT_DIR.parent
_SRC_ROOT = _REPO_ROOT / "src"
_MANIFEST = _REPO_ROOT / "eng" / "api_touchpoints.yaml"

_RE_HARMONY_TYPEOF_NAMEOF = re.compile(
    r"\[HarmonyPatch\s*\(\s*typeof\s*\(\s*(?P<type>[^)]+?)\s*\)\s*,\s*" r"nameof\s*\(\s*(?P<nameof_expr>[^)]+?)\s*\)\s*(?:,\s*[^)]*)?\)\s*\]",
    re.MULTILINE,
)
_RE_HARMONY_TYPEOF_STRING = re.compile(
    r"\[HarmonyPatch\s*\(\s*typeof\s*\(\s*(?P<type>[^)]+?)\s*\)\s*,\s*" r'"(?P<member>[^"]+)"',
    re.MULTILINE,
)
_RE_HARMONY_NAMEOF_ONLY = re.compile(
    r"\[HarmonyPatch\s*\(\s*nameof\s*\(\s*(?P<nameof_expr>[^)]+?)\s*\)\s*\)\s*\]",
    re.MULTILINE,
)
_RE_CLASS_HARMONY_TYPE = re.compile(
    r"\[HarmonyPatch\s*\(\s*typeof\s*\(\s*(?P<type>[^)]+?)\s*\)\s*\)\s*\]",
    re.MULTILINE,
)
_RE_ACCESS_TOOLS = re.compile(
    r"AccessTools\.(?P<kind>Method|Property|Field)\s*\(\s*typeof\s*\(\s*(?P<type>[^)]+?)\s*\)\s*,\s*" r'"(?P<member>[^"]+)"',
    re.MULTILINE,
)
_RE_HARMONY_TARGET_METHOD = re.compile(r"\[HarmonyTargetMethod\]")

_KITLIB_SHORT_TYPES = frozenset({"SaveSlotManager"})


def _is_sts2_touchpoint_type(type_name: str) -> bool:
    if type_name in _KITLIB_SHORT_TYPES:
        return False
    if type_name.startswith("MegaCrit."):
        return True
    return "." not in type_name


@dataclass
class Touchpoint:
    type_name: str
    member: str
    kind: str
    sources: set[str] = field(default_factory=set)
    dynamic: bool = False

    @property
    def id(self) -> str:
        short = self.type_name.split(".")[-1]
        return f"{short}.{self.member}"


def _normalize_type(type_expr: str) -> str:
    t = type_expr.strip()
    t = re.sub(r"\s+", "", t)
    return t


def _member_from_nameof(nameof_expr: str) -> str | None:
    expr = nameof_expr.strip()
    if "." not in expr:
        return None
    return expr.rsplit(".", 1)[-1].strip()


def _rel_source(path: Path) -> str:
    try:
        return path.relative_to(_REPO_ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def _scan_file(path: Path, class_types: list[str], touchpoints: dict[str, Touchpoint]) -> None:
    text = path.read_text(encoding="utf-8")
    rel = _rel_source(path)

    file_class_types = [m.group("type") for m in _RE_CLASS_HARMONY_TYPE.finditer(text)]
    default_type = file_class_types[0] if file_class_types else None

    if _RE_HARMONY_TARGET_METHOD.search(text):
        tp_id = f"dynamic:{rel}"
        touchpoints.setdefault(
            tp_id,
            Touchpoint(type_name="*", member="*", kind="method", dynamic=True),
        ).sources.add(rel)

    for m in _RE_HARMONY_TYPEOF_NAMEOF.finditer(text):
        type_name = _normalize_type(m.group("type"))
        if not _is_sts2_touchpoint_type(type_name):
            continue
        member = _member_from_nameof(m.group("nameof_expr"))
        if not member:
            continue
        tp = touchpoints.setdefault(
            f"{type_name.split('.')[-1]}.{member}",
            Touchpoint(type_name=type_name, member=member, kind="method"),
        )
        tp.type_name = type_name
        tp.member = member
        tp.sources.add(rel)

    for m in _RE_HARMONY_TYPEOF_STRING.finditer(text):
        type_name = _normalize_type(m.group("type"))
        if not _is_sts2_touchpoint_type(type_name):
            continue
        member = m.group("member")
        tp = touchpoints.setdefault(
            f"{type_name.split('.')[-1]}.{member}",
            Touchpoint(type_name=type_name, member=member, kind="method"),
        )
        tp.type_name = type_name
        tp.member = member
        tp.sources.add(rel)

    for m in _RE_HARMONY_NAMEOF_ONLY.finditer(text):
        member = _member_from_nameof(m.group("nameof_expr"))
        if not member or not default_type:
            continue
        type_name = _normalize_type(default_type)
        tp = touchpoints.setdefault(
            f"{type_name.split('.')[-1]}.{member}",
            Touchpoint(type_name=type_name, member=member, kind="method"),
        )
        tp.type_name = type_name
        tp.member = member
        tp.sources.add(rel)

    for m in _RE_ACCESS_TOOLS.finditer(text):
        type_name = _normalize_type(m.group("type"))
        if not _is_sts2_touchpoint_type(type_name):
            continue
        member = m.group("member")
        kind = m.group("kind").lower()
        tp = touchpoints.setdefault(
            f"{type_name.split('.')[-1]}.{member}",
            Touchpoint(type_name=type_name, member=member, kind=kind),
        )
        tp.type_name = type_name
        tp.member = member
        tp.kind = kind
        tp.sources.add(rel)

    _ = class_types


def _parse_yaml_simple(path: Path) -> dict:
    """Minimal parser for existing manifest profile overrides."""
    if not path.is_file():
        return {"profiles": {}, "touchpoints": {}}
    text = path.read_text(encoding="utf-8")
    profiles: dict[str, str] = {}
    overrides: dict[str, dict[str, dict[str, str]]] = {}
    current_id: str | None = None
    in_profiles_block = False
    in_touchpoint_profiles = False

    for raw in text.splitlines():
        line = raw.rstrip()
        stripped = line.strip()
        if stripped.startswith("stable:") and not current_id:
            profiles["stable"] = stripped.split(":", 1)[1].strip().strip('"')
        elif stripped.startswith("beta:") and not current_id and not in_touchpoint_profiles:
            if "touchpoints" not in raw and current_id is None:
                profiles["beta"] = stripped.split(":", 1)[1].strip().strip('"')
        elif stripped.startswith("- id:"):
            current_id = stripped.split(":", 1)[1].strip()
            overrides.setdefault(current_id, {})
            in_touchpoint_profiles = False
        elif current_id and stripped == "profiles:":
            in_touchpoint_profiles = True
        elif current_id and in_touchpoint_profiles and stripped.startswith("stable:"):
            if "member:" in stripped:
                member = stripped.split("member:", 1)[1].strip()
                overrides[current_id].setdefault("stable", {})["member"] = member
            elif stripped.endswith("stable:"):
                in_profiles_block = True
            elif stripped == "skip: true":
                overrides[current_id].setdefault("stable", {})["skip"] = "true"
        elif current_id and in_touchpoint_profiles and stripped.startswith("beta:"):
            if "member:" in stripped:
                member = stripped.split("member:", 1)[1].strip()
                overrides[current_id].setdefault("beta", {})["member"] = member
            elif stripped == "skip: true":
                overrides[current_id].setdefault("beta", {})["skip"] = "true"
        elif stripped.startswith("member:") and current_id and in_touchpoint_profiles and in_profiles_block:
            member = stripped.split(":", 1)[1].strip()
            overrides[current_id].setdefault("stable", {})["member"] = member
            in_profiles_block = False

    return {"profiles": profiles, "overrides": overrides}


def _yaml_quote(value: str) -> str:
    if re.fullmatch(r"[A-Za-z0-9_.]+", value):
        return value
    escaped = value.replace("\\", "\\\\").replace('"', '\\"')
    return f'"{escaped}"'


def _write_manifest(
    path: Path,
    touchpoints: dict[str, Touchpoint],
    profile_versions: dict[str, str],
    overrides: dict[str, dict[str, dict[str, str]]],
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines: list[str] = [
        "# KitLib game API touchpoints (generated by scripts/extract_api_touchpoints.py).",
        "# profiles.*.member overrides are preserved across extract runs — edit for cross-version renames.",
        "",
        "profiles:",
        f'  beta: "{profile_versions.get("beta", "0.109.0")}"',
        "",
        "touchpoints:",
    ]

    for tp_id in sorted(touchpoints.keys(), key=lambda k: (touchpoints[k].dynamic, k)):
        tp = touchpoints[tp_id]
        if tp.dynamic:
            lines.append(f"  - id: {tp_id}")
            lines.append("    dynamic: true")
            lines.append("    sources:")
            for src in sorted(tp.sources):
                lines.append(f"      - {src}")
            lines.append("")
            continue

        lines.append(f"  - id: {tp.id}")
        lines.append(f"    type: {_yaml_quote(tp.type_name)}")
        lines.append(f"    member: {_yaml_quote(tp.member)}")
        lines.append(f"    kind: {tp.kind}")
        ov = overrides.get(tp.id, {})
        if ov:
            lines.append("    profiles:")
            for profile in profile_versions:
                if profile not in ov:
                    continue
                prof = ov[profile]
                if "member" in prof or prof.get("skip") == "true":
                    lines.append(f"      {profile}:")
                    if prof.get("skip") == "true":
                        lines.append("        skip: true")
                    elif "member" in prof:
                        lines.append(f"        member: {_yaml_quote(prof['member'])}")
        lines.append("    sources:")
        for src in sorted(tp.sources):
            lines.append(f"      - {src}")
        lines.append("")

    path.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")


def _ensure_manual_touchpoints(touchpoints: dict[str, Touchpoint]) -> None:
    manual = Touchpoint(
        type_name="MegaCrit.Sts2.Core.Runs.RunManager",
        member="SetUpNewSingleplayer",
        kind="method",
    )
    manual.sources.add("src/KitLib.Modules.Cheat/Patches/RunStartPatch.cs")
    tp = touchpoints.setdefault(manual.id, manual)
    tp.type_name = manual.type_name
    tp.member = manual.member
    tp.kind = manual.kind
    tp.sources.add("src/KitLib.Modules.Cheat/Patches/RunStartPatch.cs")


def _ensure_manual_overrides(overrides: dict[str, dict[str, dict[str, str]]]) -> None:
    overrides.pop("CombatManager.IsPlayPhase", None)
    run = overrides.get("RunManager.SetUpNewSingleplayer")
    if run:
        run.pop("stable", None)
        run.pop("beta", None)
        if not run:
            overrides.pop("RunManager.SetUpNewSingleplayer", None)

    saved = overrides.get("RunManager.SetUpSavedSinglePlayer")
    if saved:
        saved.pop("stable", None)
        saved.pop("beta", None)
        if not saved:
            overrides.pop("RunManager.SetUpSavedSinglePlayer", None)


def main() -> int:
    ap = argparse.ArgumentParser(description="Extract KitLib API touchpoints into eng/api_touchpoints.yaml")
    ap.add_argument("--repo-root", type=Path, default=_REPO_ROOT)
    ap.add_argument("--manifest", type=Path, default=_MANIFEST)
    args = ap.parse_args()

    src_root = args.repo_root / "src"
    if not src_root.is_dir():
        print(f"src/ not found: {src_root}", file=sys.stderr)
        return 1

    existing = _parse_yaml_simple(args.manifest)
    overrides: dict[str, dict[str, dict[str, str]]] = existing.get("overrides", {})
    profile_versions = existing.get("profiles") or {"beta": "0.109.0"}

    touchpoints: dict[str, Touchpoint] = {}
    for cs in sorted(src_root.rglob("*.cs")):
        _scan_file(cs, [], touchpoints)

    _ensure_manual_touchpoints(touchpoints)
    stale = touchpoints.pop("RunManager.SetUpNewSinglePlayer", None)
    if stale:
        canonical = touchpoints["RunManager.SetUpNewSingleplayer"]
        canonical.sources.update(stale.sources)
    _ensure_manual_overrides(overrides)
    stale_saved = touchpoints.pop("RunManager.SetUpSavedSingleplayer", None)
    if stale_saved:
        canonical_saved = touchpoints.setdefault(
            "RunManager.SetUpSavedSinglePlayer",
            Touchpoint(
                type_name="RunManager",
                member="SetUpSavedSingleplayer",
                kind="method",
            ),
        )
        canonical_saved.sources.update(stale_saved.sources)
    _write_manifest(args.manifest, touchpoints, profile_versions, overrides)
    static = sum(1 for t in touchpoints.values() if not t.dynamic)
    dynamic = sum(1 for t in touchpoints.values() if t.dynamic)
    print(f"Wrote {args.manifest} ({static} static, {dynamic} dynamic touchpoints)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
