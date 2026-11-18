#!/usr/bin/env python3
"""Download the official STS2 ModUploader build for this machine.

The uploader (https://github.com/megacrit/sts2-mod-uploader) ships Windows,
macOS and Linux builds; Steam Workshop uploads previously required the Windows
``ModUploader.exe``. This script picks the asset matching the current OS/arch,
installs it under ``~/.local/share/kitlib/sts2-mod-uploader/<asset>`` and (with
``--write-env``) records its path as ``STS2_MOD_UPLOADER`` in ``.env``.

Usage:
    python scripts/setup_steam_uploader.py [--version v0.2.0] [--dest DIR]
                                           [--force] [--write-env]

After setup, run ``make upload-steam``. Steam must be running and signed in.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
import zipfile
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
_REPO = _SCRIPT_DIR.parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.dotenv import upsert_env_key  # noqa: E402
from lib.mod_uploader_paths import (  # noqa: E402
    default_install_dir,
    executable_name,
    platform_asset,
)

_RELEASES_API = "https://api.github.com/repos/megacrit/sts2-mod-uploader/releases/latest"
_RELEASE_DOWNLOAD = "https://github.com/megacrit/sts2-mod-uploader/releases/download/{tag}/{asset}"
_HTTP_TIMEOUT = 60
_KNOWN_TAGS = ("v0.2.0", "v0.1.0")


def _request(url: str) -> urllib.request.Request:
    return urllib.request.Request(url, headers={"User-Agent": "KitLib-release-tooling"})


def _latest_tag() -> str | None:
    try:
        with urllib.request.urlopen(_request(_RELEASES_API), timeout=_HTTP_TIMEOUT) as response:
            payload = json.load(response)
        tag = str(payload.get("tag_name") or "").strip()
        return tag or None
    except (OSError, ValueError, urllib.error.URLError):
        return None


def _download_zip(tag: str, asset: str, destination: Path) -> Path:
    url = _RELEASE_DOWNLOAD.format(tag=tag, asset=asset)
    destination.parent.mkdir(parents=True, exist_ok=True)
    print(f"Downloading {url}")
    with urllib.request.urlopen(_request(url), timeout=_HTTP_TIMEOUT) as response:
        total = int(response.headers.get("Content-Length") or 0)
        with destination.open("wb") as handle:
            while True:
                chunk = response.read(1024 * 256)
                if not chunk:
                    break
                handle.write(chunk)
        size = destination.stat().st_size
    suffix = f" ({size / 1_000_000:.1f} MB)" if total <= 0 else f" ({size / 1_000_000:.1f}/{total / 1_000_000:.1f} MB)"
    print(f"Downloaded{suffix}")
    return destination


def _extract(zip_path: Path, destination: Path) -> None:
    destination_resolved = destination.resolve()
    with zipfile.ZipFile(zip_path) as archive:
        for member in archive.infolist():
            target = (destination / member.filename).resolve()
            if not target.is_relative_to(destination_resolved):
                raise RuntimeError(f"Refusing path traversal in uploader zip: {member.filename}")
        archive.extractall(destination)

    executable = destination / executable_name()
    if not executable.is_file():
        raise RuntimeError(f"Uploader zip did not contain {executable_name()}")
    if not sys.platform.startswith("win"):
        executable.chmod(executable.stat().st_mode | 0o111)


def _verify(executable: Path) -> None:
    result = subprocess.run(
        [str(executable), "--help"],
        capture_output=True,
        text=True,
        timeout=30,
        check=False,
        cwd=executable.parent,
    )
    if result.returncode != 0:
        tail = (result.stderr or result.stdout or "").strip()[-600:]
        raise RuntimeError(f"{executable} failed to run (exit {result.returncode}).\n{tail}")


def _install(version: str, destination: Path, *, force: bool) -> Path:
    asset = platform_asset()
    executable = destination / executable_name()
    if executable.is_file() and not force:
        print(f"Already installed: {executable} (use --force to reinstall)")
        return executable

    tags: list[str] = []
    if version.strip():
        tags.append(version.strip())
    else:
        latest = _latest_tag()
        if latest:
            tags.append(latest)
        tags.extend(tag for tag in _KNOWN_TAGS if tag not in tags)

    with tempfile.TemporaryDirectory(prefix="kitlib-moduploader-") as temp_dir:
        temp_path = Path(temp_dir)
        for tag in tags:
            zip_path = temp_path / f"ModUploader-{asset}.zip"
            try:
                _download_zip(tag, f"ModUploader-{asset}.zip", zip_path)
            except urllib.error.HTTPError as exc:
                print(f"  {tag}: no {asset} asset (HTTP {exc.code}), trying next version")
                continue
            if destination.exists():
                shutil.rmtree(destination)
            destination.mkdir(parents=True, exist_ok=True)
            _extract(zip_path, destination)
            print(f"Installed {tag} -> {destination}")
            return destination / executable_name()
        raise RuntimeError(f"No ModUploader-{asset}.zip found for tags: {', '.join(tags)}")


def main() -> int:
    try:
        parser = argparse.ArgumentParser(description="Download the official STS2 ModUploader for this OS.")
        parser.add_argument("--version", default="", help="Release tag, e.g. v0.2.0 (default: latest)")
        parser.add_argument(
            "--dest",
            default="",
            help="Install directory (default: ~/.local/share/kitlib/sts2-mod-uploader/<asset>)",
        )
        parser.add_argument("--force", action="store_true", help="Reinstall even if the uploader is present")
        parser.add_argument("--write-env", action="store_true", help="Write STS2_MOD_UPLOADER into .env")
        args = parser.parse_args()

        destination = Path(args.dest).expanduser() if args.dest.strip() else default_install_dir()
        print(f"Platform: {platform_asset()}")
        executable = _install(args.version, destination, force=args.force)
        _verify(executable)

        print("")
        print(f"Uploader: {executable}")
        print("Add this to .env:")
        print(f"  STS2_MOD_UPLOADER={executable}")
        if args.write_env:
            changed = upsert_env_key(_REPO / ".env", "STS2_MOD_UPLOADER", str(executable))
            print(f"Updated .env: STS2_MOD_UPLOADER={executable}" if changed else ".env already up to date")
        print("")
        print("Next: make upload-steam (Steam must be running and signed in).")
        return 0
    except (RuntimeError, OSError, zipfile.BadZipFile) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
