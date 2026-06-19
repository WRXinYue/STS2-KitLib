#!/usr/bin/env python3
"""Sync KitLib into a Steam Workshop workspace and upload via ModUploader.exe.

Environment (.env):
    STS2_MOD_UPLOADER  Path to ModUploader.exe (see .env.example)

Usage:
    python scripts/publish_steam.py sync [--skip-build] [--change-note TEXT] [--unreleased]
    python scripts/publish_steam.py upload [--dry-run]

Change notes default to bilingual Steam BBCode from CHANGELOG.md + CHANGELOG.zh-CN.md
(same layout as FoxHimeMod: version header, EN / 中文 separators, markdown → BBCode).
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
_REPO = _SCRIPT_DIR.parent
_WORKSPACE = _REPO / "steam" / "workshop"
_CONTENT = _WORKSPACE / "content"
_STAGING = _REPO / "build" / "steam-stage"
_PREVIEW = _REPO / "assets" / "devmode.png"

if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.dotenv import load_dotenv  # noqa: E402
from lib.steam_changelog import get_change_note  # noqa: E402


def _read_kitlib_manifest() -> dict:
    return json.loads((_REPO / "KitLib.json").read_text(encoding="utf-8"))


def _uploader_setup_error() -> str | None:
    raw = os.environ.get("STS2_MOD_UPLOADER", "").strip()
    if not raw:
        return (
            "STS2_MOD_UPLOADER is not set.\n"
            "  Add it to .env (copy from .env.example), e.g.:\n"
            "  STS2_MOD_UPLOADER=C:\\tools\\sts2-mod-uploader\\ModUploader.exe"
        )
    path = Path(os.path.expandvars(raw)).expanduser()
    if not path.is_file():
        return (
            f"STS2_MOD_UPLOADER points to a missing file: {path}\n"
            "  Download ModUploader-win-x64.zip from:\n"
            "  https://github.com/megacrit/sts2-mod-uploader/releases/latest\n"
            "  Extract ModUploader.exe to the path above."
        )
    return None


def _resolve_uploader() -> Path:
    err = _uploader_setup_error()
    if err:
        raise RuntimeError(err)
    raw = os.environ.get("STS2_MOD_UPLOADER", "").strip()
    return Path(os.path.expandvars(raw)).expanduser().resolve()


def _stage_bundle(skip_build: bool) -> Path:
    cmd = [
        sys.executable,
        str(_SCRIPT_DIR / "package_modules.py"),
        "--stage-dir",
        str(_STAGING),
    ]
    if skip_build:
        cmd.append("--skip-build")
    subprocess.run(cmd, cwd=_REPO, check=True)
    bundle = _STAGING / "KitLib"
    if not bundle.is_dir():
        raise RuntimeError(f"Expected staged bundle at {bundle}")
    return bundle


def _patch_workshop_json(change_note: str | None, *, prefer_unreleased: bool = False) -> None:
    path = _WORKSPACE / "workshop.json"
    if not path.is_file():
        raise RuntimeError(f"Missing {_WORKSPACE.relative_to(_REPO)}/workshop.json")

    manifest = _read_kitlib_manifest()
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    data["title"] = manifest.get("name") or data.get("title") or "KitLib"
    data["description"] = manifest.get("description") or data.get("description") or ""

    if change_note and change_note.strip():
        note = change_note.strip()
    else:
        note = get_change_note(_REPO, prefer_unreleased=prefer_unreleased)
        if not note:
            raise RuntimeError(
                "ChangeNote is empty. Add content under CHANGELOG [Unreleased] or a released "
                "## [X.Y.Z] section, or pass --change-note."
            )

    data["changeNote"] = note
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    preview = _WORKSPACE / "changeNote.preview.txt"
    preview.write_text(note + "\n", encoding="utf-8")


def sync_workspace(skip_build: bool, change_note: str | None, *, prefer_unreleased: bool = False) -> None:
    _WORKSPACE.mkdir(parents=True, exist_ok=True)
    bundle = _stage_bundle(skip_build)

    if _CONTENT.exists():
        shutil.rmtree(_CONTENT)
    shutil.copytree(bundle, _CONTENT)

    if not _PREVIEW.is_file():
        raise RuntimeError(f"Missing workshop preview source: {_PREVIEW.relative_to(_REPO)}")
    shutil.copy2(_PREVIEW, _WORKSPACE / "image.png")

    _patch_workshop_json(change_note, prefer_unreleased=prefer_unreleased)
    file_count = sum(1 for p in _CONTENT.rglob("*") if p.is_file())
    print(f"Workshop content synced -> {_CONTENT.relative_to(_REPO)} ({file_count} files)")


def upload_workspace(dry_run: bool, *, optional: bool = False) -> int:
    for name in ("workshop.json", "image.png"):
        if not (_WORKSPACE / name).is_file():
            print(f"ERROR: missing {_WORKSPACE.relative_to(_REPO)}/{name}. Run: make steam-workspace", file=sys.stderr)
            return 1
    if not _CONTENT.is_dir() or not any(_CONTENT.iterdir()):
        print(f"ERROR: {_CONTENT.relative_to(_REPO)} is empty. Run: make steam-workspace", file=sys.stderr)
        return 1

    err = _uploader_setup_error()
    if err:
        if optional:
            print(f"WARN: Steam Workshop upload skipped.\n  {err}", file=sys.stderr)
            return 0
        raise RuntimeError(err)

    uploader = _resolve_uploader()
    cmd = [str(uploader), "upload", "-w", str(_WORKSPACE.resolve())]
    print("Upload command:", " ".join(f'"{part}"' if " " in part else part for part in cmd))
    if dry_run:
        print("(dry-run — not invoking ModUploader)")
        return 0

    subprocess.run(cmd, cwd=_WORKSPACE, check=True)
    return 0


def main() -> int:
    load_dotenv(_REPO / ".env")

    ap = argparse.ArgumentParser(description="Sync or upload KitLib Steam Workshop workspace.")
    sub = ap.add_subparsers(dest="command", required=True)

    sync_ap = sub.add_parser("sync", help="Build/stage mod files into steam/workshop/content/")
    sync_ap.add_argument("--skip-build", action="store_true", help="Use existing build/ artifacts")
    sync_ap.add_argument(
        "--change-note",
        default="",
        help="Override workshop.json changeNote (default: CHANGELOG.md + CHANGELOG.zh-CN.md, FoxHime format)",
    )
    sync_ap.add_argument(
        "--unreleased",
        action="store_true",
        help="Use ## [Unreleased] instead of the latest released version section",
    )

    upload_ap = sub.add_parser("upload", help="Run ModUploader.exe upload -w steam/workshop")
    upload_ap.add_argument("--dry-run", action="store_true", help="Print command only")
    upload_ap.add_argument(
        "--optional",
        action="store_true",
        help="Exit 0 with a warning if STS2_MOD_UPLOADER is missing (for upload-all)",
    )

    args = ap.parse_args()
    if args.command == "sync":
        sync_workspace(args.skip_build, args.change_note or None, prefer_unreleased=args.unreleased)
        return 0
    return upload_workspace(args.dry_run, optional=args.optional)


if __name__ == "__main__":
    raise SystemExit(main())
