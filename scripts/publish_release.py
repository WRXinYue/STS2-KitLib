#!/usr/bin/env python3
"""Build the mod and publish a GitHub Release."""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent
_SCRIPTS_DIR = Path(__file__).resolve().parent
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))

from lib.release_assets import (  # noqa: E402
    github_release_assets,
    tools_rid,
)


def _release_tag(version: str) -> str:
    return f"v{version}"


def _changelog_section(path: Path, version: str) -> str:
    if not path.is_file():
        return ""
    lines = path.read_text(encoding="utf-8").splitlines()
    found = False
    out: list[str] = []
    header_re = re.compile(rf"^## \[{re.escape(version)}\]")
    any_section_re = re.compile(r"^## \[")
    for line in lines:
        if header_re.match(line):
            found = True
            continue
        if found and any_section_re.match(line):
            break
        if found:
            out.append(line)
    while out and not out[0].strip():
        out.pop(0)
    while out and not out[-1].strip():
        out.pop()
    return "\n".join(out)


def _ensure_release_assets(version: str, rid: str) -> list[Path]:
    assets = github_release_assets(_REPO_ROOT, version, rid)
    missing = [path for path in assets if not path.is_file()]
    if not missing:
        return assets

    print("Building release assets (zip-release build-tools)...")
    env = {**os.environ, "TOOLS_RID": rid}
    subprocess.run(
        ["make", "zip-release", "build-tools"],
        cwd=_REPO_ROOT,
        env=env,
        check=True,
    )

    still_missing = [path for path in assets if not path.is_file()]
    if still_missing:
        missing_names = ", ".join(path.name for path in still_missing)
        raise FileNotFoundError(f"Release assets missing after build: {missing_names}")
    return assets


def main() -> int:
    ap = argparse.ArgumentParser(
        description="Publish GitHub Release for KitLib.",
    )
    ap.add_argument(
        "--version",
        default="",
        help="Semver, e.g. 0.2.0 (default: KitLib.json)",
    )
    ap.add_argument(
        "--tools-rid",
        default="",
        help="Runtime ID for tool zips (default: TOOLS_RID env or host default).",
    )
    args = ap.parse_args()

    version = args.version.strip()
    if not version:
        raw = (_REPO_ROOT / "KitLib.json").read_text(encoding="utf-8")
        manifest = json.loads(raw)
        version = str(manifest["version"])
        print(f"Version auto-detected from KitLib.json: {version}")

    if not shutil.which("gh"):
        print(
            "GitHub CLI (gh) not found. Install: winget install --id GitHub.cli",
            file=sys.stderr,
        )
        return 1
    if not shutil.which("dotnet"):
        print(
            "dotnet not found. Make sure .NET SDK is on PATH.",
            file=sys.stderr,
        )
        return 1

    rid = tools_rid(args.tools_rid)
    try:
        assets = _ensure_release_assets(version, rid)
    except FileNotFoundError as ex:
        print(str(ex), file=sys.stderr)
        return 1

    notes_en = _changelog_section(_REPO_ROOT / "CHANGELOG.md", version)
    notes_zh = _changelog_section(_REPO_ROOT / "CHANGELOG.zh-CN.md", version)
    if not notes_en and not notes_zh:
        print(f"No changelog section for [{version}] - release will have no notes.")
        notes = f"Release {version}"
    else:
        parts = [p for p in (notes_en, notes_zh) if p]
        if len(parts) == 2:
            notes = parts[0] + "\n\n---\n\n" + parts[1]
        else:
            notes = parts[0]

    tag = _release_tag(version)
    print(f"Creating GitHub Release {tag}...")
    for asset in assets:
        print(f"  Asset: {asset.name}")

    subprocess.run(
        ["gh", "release", "delete", tag, "--yes"],
        cwd=_REPO_ROOT,
        capture_output=True,
    )
    with tempfile.NamedTemporaryFile(
        "w",
        suffix=".md",
        delete=False,
        encoding="utf-8",
    ) as tf:
        tf.write(notes)
        notes_file = tf.name
    try:
        cmd = [
            "gh",
            "release",
            "create",
            tag,
            *[str(path) for path in assets],
            "--title",
            tag,
            "--notes-file",
            notes_file,
        ]
        r = subprocess.run(cmd, cwd=_REPO_ROOT)
    finally:
        Path(notes_file).unlink(missing_ok=True)

    if r.returncode != 0:
        print("gh release create failed", file=sys.stderr)
        return 1
    print(f"Done! GitHub Release {tag} published with {len(assets)} assets.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
