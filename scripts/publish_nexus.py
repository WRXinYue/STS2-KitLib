#!/usr/bin/env python3
"""Upload the mod zip to Nexus Mods using the v3 multipart upload API.

Environment variables (required):
    NEXUS_API_KEY       - Your personal API key from nexusmods.com/settings/api-keys
    NEXUS_FILE_GROUP_ID - The file group ID from the mod's Files tab → "API Info"

Usage:
    python scripts/publish_nexus.py [--version X.Y.Z] [--dry-run]
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent
_SCRIPTS_DIR = Path(__file__).resolve().parent
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))
_API_BASE = os.environ.get("NEXUSMODS_API_BASE", "https://api.nexusmods.com/v3").rstrip("/")


def _load_dotenv() -> None:
    """Load key=value pairs from .env in the repo root into os.environ (no-op if missing)."""
    env_file = _REPO_ROOT / ".env"
    if not env_file.is_file():
        return
    for line in env_file.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        key = key.strip()
        value = value.strip()
        if key and key not in os.environ:
            os.environ[key] = value


_load_dotenv()
_USER_AGENT = "STS2-DevMode/publish_nexus.py"

# Multipart upload: retry each part up to this many times on transient errors.
_PART_MAX_RETRIES = 3
# Poll: wait this many seconds between state checks, up to _POLL_MAX_ATTEMPTS.
_POLL_INTERVAL = 2.0
_POLL_MAX_ATTEMPTS = 60


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _changelog_section(path: Path, version: str) -> str:
    """Return the body of the ## [version] section from a Keep-a-Changelog file."""
    import re

    if not path.is_file():
        return ""
    lines = path.read_text(encoding="utf-8").splitlines()
    header_re = re.compile(rf"^## \[{re.escape(version)}\]")
    any_section_re = re.compile(r"^## \[")
    found = False
    out: list[str] = []
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


def _api_headers(api_key: str) -> dict[str, str]:
    return {
        "Content-Type": "application/json",
        "apikey": api_key,
        "User-Agent": _USER_AGENT,
    }


def _check(resp, label: str) -> dict:
    """Raise on non-2xx, return parsed JSON."""
    if not (200 <= resp.status_code < 300):
        raise RuntimeError(
            f"{label} failed — HTTP {resp.status_code}:\n{resp.text}"
        )
    return resp.json()


# ---------------------------------------------------------------------------
# Upload steps (mirrors Nexus-Mods/upload-action/src/index.ts)
# ---------------------------------------------------------------------------

def step1_create_multipart(zip_path: Path, api_key: str) -> dict:
    """POST /uploads/multipart — obtain presigned part URLs."""
    import requests

    size = zip_path.stat().st_size
    print(f"  File size: {size:,} bytes ({size / 1_048_576:.1f} MB)")
    resp = requests.post(
        f"{_API_BASE}/uploads/multipart",
        headers=_api_headers(api_key),
        json={"filename": zip_path.name, "size_bytes": str(size)},
        timeout=30,
    )
    data = _check(resp, "Create multipart upload")["data"]
    n = len(data["part_presigned_urls"])
    print(f"  Upload ID  : {data['id']}  ({n} part{'s' if n != 1 else ''})")
    return data


def step2_upload_parts(zip_path: Path, upload_info: dict) -> list[dict]:
    """PUT each file chunk to its presigned S3 URL; return list of {partNumber, etag}."""
    import requests

    part_urls: list[str] = upload_info["part_presigned_urls"]
    part_size: int = upload_info["part_size_bytes"]
    total = len(part_urls)
    results: list[dict] = []

    with open(zip_path, "rb") as fh:
        for idx, url in enumerate(part_urls):
            part_number = idx + 1
            offset = idx * part_size
            fh.seek(offset)
            chunk = fh.read(part_size)

            for attempt in range(1, _PART_MAX_RETRIES + 1):
                print(
                    f"  Part {part_number}/{total}  ({len(chunk):,} bytes)"
                    + (f"  [retry {attempt}]" if attempt > 1 else ""),
                    end="",
                    flush=True,
                )
                resp = requests.put(
                    url,
                    data=chunk,
                    headers={
                        "Content-Type": "application/octet-stream",
                        "Content-Length": str(len(chunk)),
                    },
                    timeout=120,
                )
                if resp.ok:
                    etag = resp.headers.get("ETag", "").replace('"', "")
                    if not etag:
                        raise RuntimeError(f"No ETag for part {part_number}")
                    print(f"  ✓ etag={etag[:12]}…")
                    results.append({"partNumber": part_number, "etag": etag})
                    break
                if attempt == _PART_MAX_RETRIES:
                    raise RuntimeError(
                        f"Part {part_number} upload failed after {_PART_MAX_RETRIES} attempts: "
                        f"HTTP {resp.status_code}"
                    )
                time.sleep(2 ** attempt)

    return results


def step3_complete_multipart(complete_url: str, parts: list[dict]) -> None:
    """POST S3 CompleteMultipartUpload XML to the presigned complete URL."""
    import requests

    part_xml = "\n".join(
        f"  <Part>\n    <PartNumber>{p['partNumber']}</PartNumber>\n    <ETag>{p['etag']}</ETag>\n  </Part>"
        for p in parts
    )
    xml = f"<CompleteMultipartUpload>\n{part_xml}\n</CompleteMultipartUpload>"
    resp = requests.post(
        complete_url,
        data=xml.encode(),
        headers={"Content-Type": "application/xml"},
        timeout=60,
    )
    if not resp.ok:
        raise RuntimeError(f"Complete multipart failed: HTTP {resp.status_code}\n{resp.text}")
    print("  Multipart assembly confirmed by S3.")


def step4_finalise(upload_id: str, api_key: str) -> dict:
    """POST /uploads/{id}/finalise — tell Nexus the upload is ready."""
    import requests

    resp = requests.post(
        f"{_API_BASE}/uploads/{upload_id}/finalise",
        headers=_api_headers(api_key),
        timeout=30,
    )
    return _check(resp, "Finalise upload")["data"]


def step5_poll(upload_id: str, api_key: str) -> dict:
    """GET /uploads/{id} until state == 'available'."""
    import requests

    print("  Waiting for Nexus to process the upload", end="", flush=True)
    interval = _POLL_INTERVAL
    for attempt in range(_POLL_MAX_ATTEMPTS):
        resp = requests.get(
            f"{_API_BASE}/uploads/{upload_id}",
            headers=_api_headers(api_key),
            timeout=30,
        )
        data = _check(resp, "Poll upload state")["data"]
        state = data.get("state", "unknown")
        if state == "available":
            print(" done.")
            return data
        print(".", end="", flush=True)
        time.sleep(min(interval * (1.5 ** attempt), 30))
    raise RuntimeError(f"Upload processing timed out after {_POLL_MAX_ATTEMPTS} polls.")


def step6_update_mod_file(
    upload_id: str,
    group_id: str,
    api_key: str,
    *,
    name: str,
    version: str,
    description: str = "",
    file_category: str = "main",
    archive_existing: bool = True,
    primary_mod_manager_download: bool = True,
) -> str:
    """POST /mod-file-update-groups/{group_id}/versions — attach upload to mod page."""
    import requests

    body: dict = {
        "upload_id": upload_id,
        "name": name,
        "version": version,
        "file_category": file_category,
        "archive_existing_file": archive_existing,
        "primary_mod_manager_download": primary_mod_manager_download,
    }
    if description:
        body["description"] = description

    resp = requests.post(
        f"{_API_BASE}/mod-file-update-groups/{group_id}/versions",
        headers=_api_headers(api_key),
        json=body,
        timeout=30,
    )
    result = _check(resp, "Update mod file")
    file_uid: str = result["data"]["id"]
    return file_uid


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main() -> int:
    ap = argparse.ArgumentParser(description="Upload DevMode zip to Nexus Mods.")
    ap.add_argument("--version", default="", help="Semver, e.g. 0.6.0 (default: DevMode.json)")
    ap.add_argument(
        "--dry-run",
        action="store_true",
        help="Build/package only; skip the actual Nexus upload.",
    )
    args = ap.parse_args()

    # ── resolve version ──────────────────────────────────────────────────────
    version = args.version.strip()
    if not version:
        raw = (_REPO_ROOT / "DevMode.json").read_text(encoding="utf-8")
        manifest = json.loads(raw)
        version = str(manifest["version"])
        print(f"Version auto-detected from DevMode.json: {version}")

    zip_path = _REPO_ROOT / "build" / f"DevMode-v{version}.zip"

    # ── check credentials ────────────────────────────────────────────────────
    api_key = os.environ.get("NEXUS_API_KEY", "").strip()
    group_id = os.environ.get("NEXUS_FILE_GROUP_ID", "").strip()

    if not args.dry_run:
        if not api_key:
            print(
                "ERROR: NEXUS_API_KEY environment variable is not set.\n"
                "       Get your key at https://www.nexusmods.com/settings/api-keys",
                file=sys.stderr,
            )
            return 1
        if not group_id:
            print(
                "ERROR: NEXUS_FILE_GROUP_ID environment variable is not set.\n"
                "       Find it on your mod's Files tab → API Info.",
                file=sys.stderr,
            )
            return 1

    # ── ensure zip exists ────────────────────────────────────────────────────
    if not zip_path.is_file():
        print(f"Zip not found at {zip_path} — running 'make zip' first…")
        import subprocess

        r = subprocess.run(["make", "zip"], cwd=_REPO_ROOT)
        if r.returncode != 0:
            print("make zip failed.", file=sys.stderr)
            return 1

    if not zip_path.is_file():
        print(f"ERROR: zip still missing after build: {zip_path}", file=sys.stderr)
        return 1

    # ── build file description: changelog only, converted to Nexus BBCode ─────
    from md_to_nexus import convert_markdown  # noqa: PLC0415

    notes_en = _changelog_section(_REPO_ROOT / "CHANGELOG.md", version)
    notes_zh = _changelog_section(_REPO_ROOT / "CHANGELOG.zh-CN.md", version)
    parts: list[str] = []
    if notes_en:
        parts.append(convert_markdown(notes_en))
    if notes_zh:
        parts.append(convert_markdown(notes_zh))
    description = "\n\n[line]\n\n".join(parts)

    display_name = f"DevMode v{version}"

    if args.dry_run:
        print(f"\n[dry-run] Would upload: {zip_path}")
        print(f"[dry-run]   display_name : {display_name}")
        print(f"[dry-run]   version      : {version}")
        print(f"[dry-run]   description  :\n{description[:300] or '(empty)'}")
        return 0

    # ── upload ───────────────────────────────────────────────────────────────
    try:
        import requests  # noqa: F401
    except ImportError:
        print("ERROR: 'requests' package not found. Run: pip install requests", file=sys.stderr)
        return 1

    print(f"\nUploading {zip_path.name} to Nexus Mods…")

    print("\n[1/6] Creating multipart upload…")
    upload_info = step1_create_multipart(zip_path, api_key)
    upload_id = upload_info["id"]

    print("\n[2/6] Uploading parts…")
    parts_result = step2_upload_parts(zip_path, upload_info)

    print("\n[3/6] Completing multipart upload…")
    step3_complete_multipart(upload_info["complete_presigned_url"], parts_result)

    print("\n[4/6] Finalising upload…")
    step4_finalise(upload_id, api_key)

    print("\n[5/6] Polling upload state…")
    step5_poll(upload_id, api_key)

    print("\n[6/6] Attaching file to mod page…")
    file_uid = step6_update_mod_file(
        upload_id,
        group_id,
        api_key,
        name=display_name,
        version=version,
        description=description,
        file_category="main",
        archive_existing=True,
        primary_mod_manager_download=True,
    )

    print(f"\nDone! File UID: {file_uid}")
    print(f"      DevMode v{version} is now live on Nexus Mods.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
