#!/usr/bin/env python3
"""Package and upload KitLib Steam Workshop workspace.

By default workshop.json omits minBranch/maxBranch (Steam branch targeting is unreliable).
Pass --branch-targeting to pin public-beta if you need it.

Environment:
    release.env  STS2_WORKSHOP_ID
    .env         STS2_MOD_UPLOADER path (see .env.example)

Usage:
    python scripts/publish_steam.py sync [--skip-build] [--change-note TEXT] [--unreleased]
    python scripts/publish_steam.py upload [--dry-run] [--optional]

Workspace: build/dist/workshop/

Tags, title, visibility, and dependencies come from workshop.json (repo root).
sync copies that template into the workspace and only fills changeNote
(and optional minBranch/maxBranch). Description is not uploaded — edit the
listing on Steam Workshop.
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
    _REPO / "assets" / "mod_image.png",
    _REPO / "assets" / "workshop-image.png",
)

WORKSHOP_DIR = _DIST / "workshop"
STEAM_BRANCH = ("public-beta", "public-beta")
DEFAULT_WORKSHOP_PRODUCT = "KitLib"

if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.mod_products import PRODUCTS  # noqa: E402
from lib.dotenv import load_release_config, upsert_env_key  # noqa: E402
from lib.steam_changelog import get_change_note, read_kitlib_version  # noqa: E402
from lib.steam_readme import STEAM_DESCRIPTION_MAX, steam_readme_paths  # noqa: E402

WORKSHOP_BUNDLE_PRODUCTS = frozenset(PRODUCTS)


def _normalize_product(product: str | None) -> str:
    value = (product or DEFAULT_WORKSHOP_PRODUCT).strip()
    if value not in PRODUCTS:
        raise RuntimeError(f"Unknown product {value!r}. Expected one of: {', '.join(PRODUCTS)}")
    if value not in WORKSHOP_BUNDLE_PRODUCTS:
        raise RuntimeError(
            f"Workshop sync does not support {value!r} yet. "
            f"Use one of: {', '.join(sorted(WORKSHOP_BUNDLE_PRODUCTS))}."
        )
    return value


def _bundle_cmd(*, skip_build: bool, product: str) -> list[str]:
    cmd = [
        sys.executable,
        str(_SCRIPT_DIR / "package_bundle.py"),
        "--stage-dir",
        str(_REPO / "build" / "steam-stage"),
        "-c",
        "Release",
        "--no-zip",
        "--product",
        product,
    ]
    if skip_build:
        cmd.append("--skip-build")
    return cmd


def _stage_bundle(*, skip_build: bool, product: str) -> Path:
    subprocess.run(_bundle_cmd(skip_build=skip_build, product=product), cwd=_REPO, check=True)
    bundle = _REPO / "build" / "steam-stage" / product
    if not bundle.is_dir():
        raise RuntimeError(f"Expected staged bundle at {bundle}")
    return bundle


def _resolve_preview_image(product: str = DEFAULT_WORKSHOP_PRODUCT) -> Path:
    if product != DEFAULT_WORKSHOP_PRODUCT:
        candidates = (
            _REPO / "mods" / product / "mod_image.png",
            _REPO / "mods" / product / "src" / "Panel" / "Assets" / "mod_image.png",
        )
        for candidate in candidates:
            if candidate.is_file():
                return candidate
        raise RuntimeError(f"Missing workshop preview image for {product}.")
    for candidate in _PREVIEW_CANDIDATES:
        if candidate.is_file():
            return candidate
    raise RuntimeError("Missing workshop preview image. Add assets/mod_image.png or assets/workshop-image.png.")


def _resolve_change_note(
    change_note: str | None,
    *,
    prefer_unreleased: bool,
    product: str = DEFAULT_WORKSHOP_PRODUCT,
) -> str:
    if change_note and change_note.strip():
        return change_note.strip()
    if product == DEFAULT_WORKSHOP_PRODUCT:
        changelog_en = _REPO / "CHANGELOG.md"
        changelog_zh = _REPO / "CHANGELOG.zh-CN.md"
        version = read_kitlib_version(_REPO)
    else:
        changelog_en = _REPO / "mods" / product / "CHANGELOG.md"
        changelog_zh = _REPO / "mods" / product / "CHANGELOG.zh-CN.md"
        version = ""
        manifest = PRODUCTS[product].manifest_path
        if manifest.is_file():
            version = str(json.loads(manifest.read_text(encoding="utf-8")).get("version") or "").strip()
    note = get_change_note(
        _REPO,
        changelog_en=changelog_en,
        changelog_zh=changelog_zh,
        prefer_unreleased=prefer_unreleased,
        version=version or None,
    )
    if note:
        return note
    if version:
        return f"[b] v{version} [/b]"
    raise RuntimeError(
        "ChangeNote is empty. Add content under CHANGELOG [Unreleased] or a released "
        "## [X.Y.Z] section, or pass --change-note."
    )


def _workshop_template(product: str) -> Path:
    if product == DEFAULT_WORKSHOP_PRODUCT:
        return _REPO / "workshop.json"
    path = _REPO / "mods" / product / "workshop.json"
    if not path.is_file():
        raise RuntimeError(f"Missing workshop.json for {product}: {path}")
    return path


def _readme_draft_paths(product: str) -> tuple[Path, Path]:
    _, _, out_en, out_zh = steam_readme_paths(_REPO, product)
    return out_en, out_zh


def _write_workshop_json(
    workspace: Path,
    change_note: str | None,
    *,
    product: str = DEFAULT_WORKSHOP_PRODUCT,
    prefer_unreleased: bool = False,
    branch_targeting: bool = False,
) -> None:
    base_note = _resolve_change_note(
        change_note,
        prefer_unreleased=prefer_unreleased,
        product=product,
    )
    resolved_note = base_note
    template = _workshop_template(product)
    if not template.is_file():
        raise RuntimeError(f"Missing workshop.json for {product}.")
    workshop = json.loads(template.read_text(encoding="utf-8-sig"))
    if not str(workshop.get("title") or "").strip():
        workshop["title"] = product
    workshop["changeNote"] = resolved_note
    if branch_targeting:
        min_branch, max_branch = STEAM_BRANCH
        workshop["minBranch"] = min_branch
        workshop["maxBranch"] = max_branch
    (workspace / "workshop.json").write_text(
        json.dumps(workshop, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    (workspace / "changeNote.preview.txt").write_text(resolved_note + "\n", encoding="utf-8")
    draft_en, draft_zh = _readme_draft_paths(product)
    preview_parts: list[str] = []
    for path in (draft_en, draft_zh):
        if path.is_file() and path.read_text(encoding="utf-8").strip():
            text = path.read_text(encoding="utf-8").strip()
            if len(text) > STEAM_DESCRIPTION_MAX:
                print(
                    f"WARN: {path.relative_to(_REPO)} is {len(text)} chars " f"(Steam limit {STEAM_DESCRIPTION_MAX}).",
                    file=sys.stderr,
                )
            preview_parts.append(f"--- {path.name} ---\n{text}")
    if preview_parts:
        description_preview = "\n\n".join(preview_parts)
    else:
        description_preview = (
            "(omitted from upload — edit listing on Steam Workshop; "
            f"optional drafts: {draft_en.relative_to(_REPO)}, {draft_zh.relative_to(_REPO)}; "
            "regenerate with: make readme-steam PRODUCT="
            f"{product})"
        )
    (workspace / "description.preview.txt").write_text(description_preview + "\n", encoding="utf-8")


def _workshop_id_env_key(product: str) -> str:
    if product == DEFAULT_WORKSHOP_PRODUCT:
        return "STS2_WORKSHOP_ID"
    return f"STS2_WORKSHOP_ID_{product.upper()}"


def _workshop_id(product: str = DEFAULT_WORKSHOP_PRODUCT) -> str:
    key = _workshop_id_env_key(product)
    value = os.environ.get(key, "").strip()
    if value:
        return value
    raise RuntimeError(
        f"{key} is not set in release.env / .env. "
        f"Add the Steam Workshop item id for {product}."
    )


def _sync_mod_id_file(workspace: Path, *, product: str = DEFAULT_WORKSHOP_PRODUCT) -> None:
    key = _workshop_id_env_key(product)
    try:
        workshop_id = _workshop_id(product)
    except RuntimeError:
        print(
            f"WARN: {key} is not set; ModUploader may create a new Workshop item for {product}.",
            file=sys.stderr,
        )
        mod_id_file = workspace / "mod_id.txt"
        if mod_id_file.is_file():
            mod_id_file.unlink()
        return
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


def sync_workspace(
    skip_build: bool,
    change_note: str | None,
    *,
    product: str = DEFAULT_WORKSHOP_PRODUCT,
    prefer_unreleased: bool = False,
    branch_targeting: bool = False,
) -> Path:
    product = _normalize_product(product)
    workspace = WORKSHOP_DIR
    content = workspace / "content"
    bundle = _stage_bundle(skip_build=skip_build, product=product)

    if content.exists():
        shutil.rmtree(content)
    shutil.copytree(bundle, content)

    shutil.copy2(_resolve_preview_image(product), workspace / "image.png")
    _write_workshop_json(
        workspace,
        change_note,
        product=product,
        prefer_unreleased=prefer_unreleased,
        branch_targeting=branch_targeting,
    )
    _sync_mod_id_file(workspace, product=product)
    (workspace / "product.txt").write_text(product + "\n", encoding="utf-8")

    file_count = sum(1 for p in content.rglob("*") if p.is_file())
    print(f"Workshop synced -> {workspace.relative_to(_REPO)} ({file_count} files)")
    return workspace


def _uploader_setup_error() -> str | None:
    raw = os.environ.get("STS2_MOD_UPLOADER", "").strip()
    if not raw:
        return "STS2_MOD_UPLOADER is not set.\n" "  Add it to .env (copy from .env.example), e.g.:\n" "  STS2_MOD_UPLOADER=C:\\tools\\sts2-mod-uploader\\ModUploader.exe"
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


def _persist_workshop_id(mod_id: str, *, product: str = DEFAULT_WORKSHOP_PRODUCT) -> None:
    mod_id = mod_id.strip()
    if not mod_id:
        return

    key = _workshop_id_env_key(product)
    current = os.environ.get(key, "").strip()
    if current == mod_id:
        return

    os.environ[key] = mod_id

    release_path = _REPO / "release.env"
    dotenv_path = _REPO / ".env"
    if upsert_env_key(release_path, key, mod_id):
        print(f"Updated {release_path.relative_to(_REPO)}: {key}={mod_id}")
    if dotenv_path.is_file() and upsert_env_key(dotenv_path, key, mod_id):
        print(f"Updated {dotenv_path.relative_to(_REPO)}: {key}={mod_id}")


def _read_synced_product(workspace: Path) -> str:
    path = workspace / "product.txt"
    if path.is_file():
        value = path.read_text(encoding="utf-8").strip()
        if value in PRODUCTS:
            return value
    return DEFAULT_WORKSHOP_PRODUCT


def upload_workspace(dry_run: bool, *, branch_targeting: bool = False) -> int:
    workspace = WORKSHOP_DIR
    product = _read_synced_product(workspace)
    for name in ("workshop.json", "image.png"):
        if not (workspace / name).is_file():
            print(
                f"ERROR: missing {workspace.relative_to(_REPO)}/{name}. Run: make workshop",
                file=sys.stderr,
            )
            return 1
    if not branch_targeting:
        _clear_branch_targeting(workspace)
    content = workspace / "content"
    if not content.is_dir() or not any(content.iterdir()):
        print(
            f"ERROR: {content.relative_to(_REPO)} is empty. Run: make workshop",
            file=sys.stderr,
        )
        return 1

    uploader = _resolve_uploader()
    cmd = [str(uploader), "upload", "-w", str(workspace.resolve())]
    print("Upload workshop:", " ".join(f'"{part}"' if " " in part else part for part in cmd))
    if dry_run:
        print("(dry-run — not invoking ModUploader)")
        return 0

    subprocess.run(cmd, cwd=workspace, check=True)

    mod_id_file = workspace / "mod_id.txt"
    if mod_id_file.is_file():
        mod_id = mod_id_file.read_text(encoding="utf-8").strip()
        if mod_id:
            _persist_workshop_id(mod_id, product=product)

    return 0


def upload_workspace_cmd(dry_run: bool, *, optional: bool = False, branch_targeting: bool = False) -> int:
    err = _uploader_setup_error()
    if err:
        if optional:
            print(f"WARN: Steam Workshop upload skipped.\n  {err}", file=sys.stderr)
            return 0
        raise RuntimeError(err)

    return upload_workspace(dry_run, branch_targeting=branch_targeting)


def main() -> int:
    load_release_config(_REPO)

    ap = argparse.ArgumentParser(description="Sync or upload KitLib Steam Workshop workspace.")
    sub = ap.add_subparsers(dest="command", required=True)

    sync_ap = sub.add_parser("sync", help="Build and stage build/dist/workshop/")
    sync_ap.add_argument("--skip-build", action="store_true", help="Use existing build/ artifacts")
    sync_ap.add_argument(
        "--product",
        default=DEFAULT_WORKSHOP_PRODUCT,
        help="Product to build and stage (default: KitLib)",
    )
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
        "--branch-targeting",
        action="store_true",
        help="Set minBranch/maxBranch to public-beta (default: omit branch targeting)",
    )
    sync_ap.add_argument(
        "--no-branch-targeting",
        action="store_true",
        help=argparse.SUPPRESS,
    )

    upload_ap = sub.add_parser("upload", help="Run ModUploader.exe for the workshop workspace")
    upload_ap.add_argument("--dry-run", action="store_true", help="Print command only")
    upload_ap.add_argument(
        "--optional",
        action="store_true",
        help="Exit 0 with a warning if STS2_MOD_UPLOADER is missing (for upload-all)",
    )
    upload_ap.add_argument(
        "--branch-targeting",
        action="store_true",
        help="Keep minBranch/maxBranch in workshop.json (default: strip before upload)",
    )
    upload_ap.add_argument(
        "--no-branch-targeting",
        action="store_true",
        help=argparse.SUPPRESS,
    )

    args = ap.parse_args()
    if args.command == "sync":
        branch_targeting = args.branch_targeting and not args.no_branch_targeting
        sync_workspace(
            args.skip_build,
            args.change_note or None,
            product=args.product,
            prefer_unreleased=args.unreleased,
            branch_targeting=branch_targeting,
        )
        return 0
    branch_targeting = args.branch_targeting and not args.no_branch_targeting
    return upload_workspace_cmd(
        args.dry_run,
        optional=args.optional,
        branch_targeting=branch_targeting,
    )


if __name__ == "__main__":
    raise SystemExit(main())
