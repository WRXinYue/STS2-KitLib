#!/usr/bin/env python3
"""Package and upload KitLib Steam Workshop workspaces (stable + beta branches).

One Workshop item (STS2_WORKSHOP_ID) can target multiple game branches via minBranch/maxBranch.

Environment:
    release.env  STS2_WORKSHOP_ID
    .env         STS2_MOD_UPLOADER path (see .env.example)

Usage:
    python scripts/publish_steam.py sync [stable|beta|all] [--skip-build] [--change-note TEXT] [--unreleased]
    python scripts/publish_steam.py upload [stable|beta|all] [--dry-run] [--optional]

Workspaces: build/dist/workshop-stable/, build/dist/workshop-beta/

workshop.json omits description on first sync; later syncs preserve a local copy only.
Steam listing text is not updated by sync/upload — edit it on the Workshop page.
Optional local drafts (manual paste on Steam Workshop):
  assets/readme.steam.en.txt
  assets/readme.steam.zh-CN.txt
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
_DIST = _REPO / "build" / "dist"
_PREVIEW_CANDIDATES = (
    _REPO / "assets" / "devmode.png",
    _REPO / "assets" / "workshop-image.png",
)

PROFILE_BRANCH = {
    "stable": ("public", "public"),
    "beta": ("public-beta", "public-beta"),
}

if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.dotenv import load_release_config, upsert_env_key  # noqa: E402
from lib.steam_changelog import get_change_note, read_kitlib_version  # noqa: E402
from lib.bundle_build import build_bundle  # noqa: E402
from lib.steam_readme import STEAM_DESCRIPTION_MAX  # noqa: E402


def _profiles(selection: str) -> list[str]:
    if selection == "all":
        return ["stable", "beta"]
    return [selection]


def _workspace_dir(profile: str) -> Path:
    return _DIST / f"workshop-{profile}"


def _run_profile_build(profile: str) -> None:
    sts2_dir = subprocess.check_output(
        [sys.executable, str(_SCRIPT_DIR / "resolve_sts2_profile_dir.py"), profile],
        text=True,
    ).strip()
    build_bundle(configuration="Release", sts2_profile=profile, sts2_dir=sts2_dir)


def _stage_bundle(skip_build: bool) -> Path:
    staging = _REPO / "build" / "steam-stage"
    cmd = [
        sys.executable,
        str(_SCRIPT_DIR / "package_modules.py"),
        "--stage-dir",
        str(staging),
    ]
    if skip_build:
        cmd.append("--skip-build")
    subprocess.run(cmd, cwd=_REPO, check=True)
    bundle = staging / "KitLib"
    if not bundle.is_dir():
        raise RuntimeError(f"Expected staged bundle at {bundle}")
    return bundle


def _resolve_preview_image() -> Path:
    for candidate in _PREVIEW_CANDIDATES:
        if candidate.is_file():
            return candidate
    raise RuntimeError(
        "Missing workshop preview image. Add assets/devmode.png or assets/workshop-image.png."
    )


def _resolve_change_note(
    change_note: str | None,
    *,
    profile: str,
    prefer_unreleased: bool,
) -> str:
    if change_note and change_note.strip():
        return change_note.strip()
    if profile == "beta":
        version = read_kitlib_version(_REPO)
        if not version:
            raise RuntimeError("KitLib.json version is missing; cannot build beta changeNote.")
        return f"[b] v{version} [/b]"
    note = get_change_note(_REPO, prefer_unreleased=prefer_unreleased)
    if not note:
        raise RuntimeError(
            "ChangeNote is empty. Add content under CHANGELOG [Unreleased] or a released "
            "## [X.Y.Z] section, or pass --change-note."
        )
    return note


def _write_workshop_json(
    workspace: Path,
    profile: str,
    change_note: str | None,
    *,
    prefer_unreleased: bool = False,
    branch_targeting: bool = True,
) -> None:
    base_note = _resolve_change_note(
        change_note,
        profile=profile,
        prefer_unreleased=prefer_unreleased,
    )
    resolved_note = base_note
    workshop: dict[str, object] = {
        "title": "KitLib",
        "visibility": "public",
        "changeNote": resolved_note,
        "tags": ["Tools & APIs"],
        "dependencies": [],
        "contentDescriptors": [],
    }
    if branch_targeting:
        min_branch, max_branch = PROFILE_BRANCH[profile]
        workshop["minBranch"] = min_branch
        workshop["maxBranch"] = max_branch
    (workspace / "workshop.json").write_text(
        json.dumps(workshop, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    (workspace / "changeNote.preview.txt").write_text(resolved_note + "\n", encoding="utf-8")
    draft_en = _REPO / "assets" / "readme.steam.en.txt"
    draft_zh = _REPO / "assets" / "readme.steam.zh-CN.txt"
    preview_parts: list[str] = []
    for path in (draft_en, draft_zh):
        if path.is_file() and path.read_text(encoding="utf-8").strip():
            text = path.read_text(encoding="utf-8").strip()
            if len(text) > STEAM_DESCRIPTION_MAX:
                print(
                    f"WARN: {path.relative_to(_REPO)} is {len(text)} chars "
                    f"(Steam limit {STEAM_DESCRIPTION_MAX}).",
                    file=sys.stderr,
                )
            preview_parts.append(f"--- {path.name} ---\n{text}")
    if preview_parts:
        description_preview = "\n\n".join(preview_parts)
    else:
        description_preview = (
            "(omitted from upload — edit listing on Steam Workshop; "
            "optional drafts: assets/readme.steam.en.txt, assets/readme.steam.zh-CN.txt)"
        )
    (workspace / "description.preview.txt").write_text(description_preview + "\n", encoding="utf-8")


def _workshop_id() -> str:
    value = os.environ.get("STS2_WORKSHOP_ID", "").strip()
    if value:
        return value
    raise RuntimeError("STS2_WORKSHOP_ID is not set in release.env / .env.")


def _sync_mod_id_file(workspace: Path) -> None:
    workshop_id = _workshop_id()
    (workspace / "mod_id.txt").write_text(workshop_id + "\n", encoding="utf-8")


def _clear_branch_targeting(workspace: Path) -> None:
    path = workspace / "workshop.json"
    if not path.is_file():
        return
    workshop = json.loads(path.read_text(encoding="utf-8"))
    workshop.pop("minBranch", None)
    workshop.pop("maxBranch", None)
    path.write_text(json.dumps(workshop, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Cleared minBranch/maxBranch in {path.relative_to(_REPO)}")


def sync_profile(
    profile: str,
    skip_build: bool,
    change_note: str | None,
    *,
    prefer_unreleased: bool = False,
    branch_targeting: bool = True,
) -> Path:
    workspace = _workspace_dir(profile)
    content = workspace / "content"
    if not skip_build:
        _run_profile_build(profile)
    bundle = _stage_bundle(skip_build=True)

    if content.exists():
        shutil.rmtree(content)
    shutil.copytree(bundle, content)

    shutil.copy2(_resolve_preview_image(), workspace / "image.png")
    _write_workshop_json(
        workspace,
        profile,
        change_note,
        prefer_unreleased=prefer_unreleased,
        branch_targeting=branch_targeting,
    )
    _sync_mod_id_file(workspace)

    file_count = sum(1 for p in content.rglob("*") if p.is_file())
    print(f"Workshop-{profile} synced -> {workspace.relative_to(_REPO)} ({file_count} files)")
    return workspace


def sync_workspaces(
    selection: str,
    skip_build: bool,
    change_note: str | None,
    *,
    prefer_unreleased: bool = False,
    branch_targeting: bool = True,
) -> None:
    for profile in _profiles(selection):
        sync_profile(
            profile,
            skip_build,
            change_note,
            prefer_unreleased=prefer_unreleased,
            branch_targeting=branch_targeting,
        )


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


def _persist_workshop_id(mod_id: str) -> None:
    mod_id = mod_id.strip()
    if not mod_id:
        return

    current = os.environ.get("STS2_WORKSHOP_ID", "").strip()
    if current == mod_id:
        return

    os.environ["STS2_WORKSHOP_ID"] = mod_id

    release_path = _REPO / "release.env"
    dotenv_path = _REPO / ".env"
    if upsert_env_key(release_path, "STS2_WORKSHOP_ID", mod_id):
        print(f"Updated {release_path.relative_to(_REPO)}: STS2_WORKSHOP_ID={mod_id}")
    if dotenv_path.is_file() and upsert_env_key(dotenv_path, "STS2_WORKSHOP_ID", mod_id):
        print(f"Updated {dotenv_path.relative_to(_REPO)}: STS2_WORKSHOP_ID={mod_id}")


def upload_profile(profile: str, dry_run: bool, *, branch_targeting: bool = True) -> int:
    workspace = _workspace_dir(profile)
    for name in ("workshop.json", "image.png"):
        if not (workspace / name).is_file():
            print(
                f"ERROR: missing {workspace.relative_to(_REPO)}/{name}. "
                f"Run: make workshop-{profile}",
                file=sys.stderr,
            )
            return 1
    if not branch_targeting:
        _clear_branch_targeting(workspace)
    content = workspace / "content"
    if not content.is_dir() or not any(content.iterdir()):
        print(
            f"ERROR: {content.relative_to(_REPO)} is empty. Run: make workshop-{profile}",
            file=sys.stderr,
        )
        return 1

    uploader = _resolve_uploader()
    cmd = [str(uploader), "upload", "-w", str(workspace.resolve())]
    print(f"Upload workshop-{profile}:", " ".join(f'"{part}"' if " " in part else part for part in cmd))
    if dry_run:
        print("(dry-run — not invoking ModUploader)")
        return 0

    subprocess.run(cmd, cwd=workspace, check=True)

    mod_id_file = workspace / "mod_id.txt"
    if mod_id_file.is_file():
        mod_id = mod_id_file.read_text(encoding="utf-8").strip()
        if mod_id:
            _persist_workshop_id(mod_id)

    return 0


def upload_workspaces(selection: str, dry_run: bool, *, optional: bool = False, branch_targeting: bool = True) -> int:
    err = _uploader_setup_error()
    if err:
        if optional:
            print(f"WARN: Steam Workshop upload skipped.\n  {err}", file=sys.stderr)
            return 0
        raise RuntimeError(err)

    exit_code = 0
    for profile in _profiles(selection):
        code = upload_profile(profile, dry_run, branch_targeting=branch_targeting)
        if code != 0:
            exit_code = code
    return exit_code


def main() -> int:
    load_release_config(_REPO)

    ap = argparse.ArgumentParser(description="Sync or upload KitLib Steam Workshop workspaces.")
    sub = ap.add_subparsers(dest="command", required=True)

    sync_ap = sub.add_parser("sync", help="Build and stage build/dist/workshop-{profile}/")
    sync_ap.add_argument(
        "profile",
        nargs="?",
        default="all",
        choices=["stable", "beta", "all"],
        help="STS2 API profile / Steam branch (default: all)",
    )
    sync_ap.add_argument("--skip-build", action="store_true", help="Use existing build/ artifacts")
    sync_ap.add_argument(
        "--change-note",
        default="",
        help="Override workshop.json changeNote (default: CHANGELOG.md + CHANGELOG.zh-CN.md)",
    )
    sync_ap.add_argument(
        "--unreleased",
        action="store_true",
        help="Use ## [Unreleased] instead of the latest released version section",
    )
    sync_ap.add_argument(
        "--no-branch-targeting",
        action="store_true",
        help="Omit minBranch/maxBranch from workshop.json (upload test / legacy items)",
    )

    upload_ap = sub.add_parser("upload", help="Run ModUploader.exe for workshop workspace(s)")
    upload_ap.add_argument(
        "profile",
        nargs="?",
        default="all",
        choices=["stable", "beta", "all"],
        help="Which workspace to upload (default: all)",
    )
    upload_ap.add_argument("--dry-run", action="store_true", help="Print command only")
    upload_ap.add_argument(
        "--optional",
        action="store_true",
        help="Exit 0 with a warning if STS2_MOD_UPLOADER is missing (for upload-all)",
    )
    upload_ap.add_argument(
        "--no-branch-targeting",
        action="store_true",
        help="Strip minBranch/maxBranch from workshop.json before upload",
    )

    args = ap.parse_args()
    if args.command == "sync":
        sync_workspaces(
            args.profile,
            args.skip_build,
            args.change_note or None,
            prefer_unreleased=args.unreleased,
            branch_targeting=not args.no_branch_targeting,
        )
        return 0
    return upload_workspaces(
        args.profile,
        args.dry_run,
        optional=args.optional,
        branch_targeting=not args.no_branch_targeting,
    )


if __name__ == "__main__":
    raise SystemExit(main())
