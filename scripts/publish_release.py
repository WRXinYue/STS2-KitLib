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


def _sts2_beta_version(raw: str) -> str:
    return raw.strip().lstrip("v") or "0.105.1"


def _resolve_sts2_beta_version(cli_value: str) -> str:
    if cli_value.strip():
        return _sts2_beta_version(cli_value)
    return _sts2_beta_version(os.environ.get("STS2_GAME_BETA_VERSION", "0.105.1"))


def _zip_name(version: str, *, beta: bool, sts2_beta_version: str) -> str:
    if beta:
        return f"KitLib-v{version}-sts2beta-v{sts2_beta_version}.zip"
    return f"KitLib-v{version}.zip"


def _release_tag(version: str, *, beta: bool, sts2_beta_version: str) -> str:
    if beta:
        return f"v{version}-sts2beta-v{sts2_beta_version}"
    return f"v{version}"


def _beta_notice_md(sts2_beta_version: str) -> str:
    return f"> **Requires Slay the Spire 2 Steam beta branch v{sts2_beta_version}.** " "Not for the public/stable build.\n\n"


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


def main() -> int:
    ap = argparse.ArgumentParser(
        description="Publish GitHub Release for DevMode.",
    )
    ap.add_argument(
        "--version",
        default="",
        help="Semver, e.g. 0.2.0 (default: KitLib.json)",
    )
    ap.add_argument(
        "--beta",
        action="store_true",
        help="Build/package/publish the STS2 Steam beta build (make zip-beta).",
    )
    ap.add_argument(
        "--sts2-beta-version",
        default="",
        help="STS2 game beta version label, e.g. 0.105.1 (default: STS2_GAME_BETA_VERSION env or 0.105.1).",
    )
    args = ap.parse_args()

    version = args.version.strip()
    if not version:
        raw = (_REPO_ROOT / "KitLib.json").read_text(encoding="utf-8")
        manifest = json.loads(raw)
        version = str(manifest["version"])
        print(f"Version auto-detected from KitLib.json: {version}")

    sts2_beta_version = _resolve_sts2_beta_version(args.sts2_beta_version)
    if args.beta:
        print(f"STS2 Steam beta branch game version: v{sts2_beta_version}")

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

    make_target = "zip-beta" if args.beta else "zip"
    print(f"Building and packaging (make {make_target})...")
    subprocess.run(["make", make_target], cwd=_REPO_ROOT, check=True)

    zip_name = _zip_name(version, beta=args.beta, sts2_beta_version=sts2_beta_version)
    zip_path = _REPO_ROOT / "build" / zip_name
    if not zip_path.is_file():
        print(f"Zip not found: {zip_path}", file=sys.stderr)
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

    if args.beta:
        notes = _beta_notice_md(sts2_beta_version) + notes

    tag = _release_tag(version, beta=args.beta, sts2_beta_version=sts2_beta_version)
    print(f"Creating GitHub Release {tag}...")
    print(f"  Assets: {zip_path}")

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
        r = subprocess.run(
            [
                "gh",
                "release",
                "create",
                tag,
                str(zip_path),
                "--title",
                tag,
                "--notes-file",
                notes_file,
            ],
            cwd=_REPO_ROOT,
        )
    finally:
        Path(notes_file).unlink(missing_ok=True)

    if r.returncode != 0:
        print("gh release create failed", file=sys.stderr)
        return 1
    print(f"Done! GitHub Release {tag} published.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
