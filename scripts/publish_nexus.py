#!/usr/bin/env python3
"""Upload the mod zip to Nexus Mods using the v3 multipart upload API.

Environment variables (required):
    NEXUS_API_KEY              - Your personal API key (.env)
    NEXUS_FILE_GROUP_ID        - Main file group ID (release.env)
    NEXUS_FILE_GROUP_ID_MCP    - Optional MCP proxy file group (release.env)
    NEXUS_FILE_GROUP_ID_KITLOG - Optional KitLog CLI file group (release.env)

Usage:
    python scripts/publish_nexus.py [--version X.Y.Z] [--mcp | --kitlog] [--dry-run]
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent


def _zip_path(version: str) -> Path:
    return _REPO_ROOT / "build" / f"KitLib-v{version}.zip"


def _tools_rid(cli_value: str) -> str:
    if cli_value.strip():
        return cli_value.strip()
    env = os.environ.get("TOOLS_RID", "").strip()
    if env:
        return env
    return "win-x64" if os.name == "nt" else "linux-x64"


def _mcp_zip_path(version: str, tools_rid: str) -> Path:
    return _REPO_ROOT / "build" / f"KitLib.Mcp-v{version}-{tools_rid}.zip"


def _kitlog_zip_path(version: str, tools_rid: str) -> Path:
    return _REPO_ROOT / "build" / f"KitLog.Cli-v{version}-{tools_rid}.zip"


def _mcp_description_bbcode(tools_rid: str) -> str:
    exe = "KitLib.Mcp.exe" if tools_rid.startswith("win") else "KitLib.Mcp"
    return (
        "[b]KitLib MCP stdio proxy[/b] (optional dev tool)\n\n"
        "Connect MCP clients (Cursor, Claude Desktop, etc.) to a running Slay the Spire 2 session "
        "with KitLib loaded.\n\n"
        f"Extract [code]{exe}[/code] and point your MCP client at it with [code]--port 9877[/code]. "
        "Full setup: GitHub README → MCP section.\n\n"
        "Requires KitLib installed and the game running."
    )


def _kitlog_description_bbcode(tools_rid: str) -> str:
    exe = "kitlog.exe" if tools_rid.startswith("win") else "kitlog"
    return (
        "[b]KitLog CLI[/b] (optional terminal log viewer)\n\n"
        "Tail KitLib session logs from a terminal. Use [code]kitlog attach[/code] for live structured "
        "logs over a named pipe while the game runs; [code]kitlog tail[/code] reads [code]session.log[/code] "
        "offline.\n\n"
        f"Extract [code]{exe}[/code] to [code]PATH[/code] or [code]mods/KitLib/tools/[/code]. "
        "The in-game log viewer can launch attach automatically.\n\n"
        "Full usage: GitHub README → KitLog CLI section."
    )


def _resolve_nexus_group_id(*, tool: str | None) -> str:
    if tool == "mcp":
        return os.environ.get("NEXUS_FILE_GROUP_ID_MCP", "").strip()
    if tool == "kitlog":
        return os.environ.get("NEXUS_FILE_GROUP_ID_KITLOG", "").strip()
    return os.environ.get("NEXUS_FILE_GROUP_ID", "").strip()


def _nexus_display_name(
    version: str,
    *,
    tool: str | None,
    tools_rid: str,
) -> str:
    if tool == "mcp":
        return f"KitLib.Mcp v{version} ({tools_rid})"
    if tool == "kitlog":
        return f"KitLog v{version} ({tools_rid})"
    return f"KitLib v{version}"


def _nexus_attach_options(*, tool: str | None) -> dict[str, object]:
    if tool:
        return {
            "file_category": "optional",
            "archive_existing": True,
            "primary_mod_manager_download": False,
        }
    return {
        "file_category": "main",
        "archive_existing": True,
        "primary_mod_manager_download": True,
    }


def _zip_path_for(version: str, *, tool: str | None, tools_rid: str) -> Path:
    if tool == "mcp":
        return _mcp_zip_path(version, tools_rid)
    if tool == "kitlog":
        return _kitlog_zip_path(version, tools_rid)
    return _zip_path(version)


def _make_target_for(*, tool: str | None) -> str:
    if tool == "mcp":
        return "zip-mcp"
    if tool == "kitlog":
        return "zip-kitlog"
    return "zip"


def _tool_env_hint(tool: str) -> tuple[str, str]:
    if tool == "mcp":
        return (
            "NEXUS_FILE_GROUP_ID_MCP",
            "Create an Optional file for KitLib.Mcp on your mod page first, "
            "then copy its group ID from API Info.",
        )
    return (
        "NEXUS_FILE_GROUP_ID_KITLOG",
        "Create an Optional file for KitLog on your mod page first, "
        "then copy its group ID from API Info.",
    )


_SCRIPTS_DIR = Path(__file__).resolve().parent
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))

from lib.dotenv import load_release_config  # noqa: E402

load_release_config(_REPO_ROOT)
_API_BASE = os.environ.get("NEXUSMODS_API_BASE", "https://api.nexusmods.com/v3").rstrip("/")
_USER_AGENT = "STS2-KitLib/publish_nexus.py"

# Multipart upload: retry each part up to this many times on transient errors.
_PART_MAX_RETRIES = 5
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
        raise RuntimeError(f"{label} failed — HTTP {resp.status_code}:\n{resp.text}")
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
    from requests.exceptions import RequestException

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
                    f"  Part {part_number}/{total}  ({len(chunk):,} bytes)" + (f"  [retry {attempt}]" if attempt > 1 else ""),
                    end="",
                    flush=True,
                )
                try:
                    resp = requests.put(
                        url,
                        data=chunk,
                        headers={
                            "Content-Type": "application/octet-stream",
                            "Content-Length": str(len(chunk)),
                        },
                        timeout=300,
                    )
                except RequestException as ex:
                    if attempt == _PART_MAX_RETRIES:
                        raise RuntimeError(
                            f"Part {part_number} upload failed after {_PART_MAX_RETRIES} attempts: {ex}"
                        ) from ex
                    print("  network error, retrying...")
                    time.sleep(2**attempt)
                    continue
                if resp.ok:
                    etag = resp.headers.get("ETag", "").replace('"', "")
                    if not etag:
                        raise RuntimeError(f"No ETag for part {part_number}")
                    print(f"  OK etag={etag[:12]}...")
                    results.append({"partNumber": part_number, "etag": etag})
                    break
                if attempt == _PART_MAX_RETRIES:
                    raise RuntimeError(f"Part {part_number} upload failed after {_PART_MAX_RETRIES} attempts: " f"HTTP {resp.status_code}")
                print(f"  HTTP {resp.status_code}, retrying...")
                time.sleep(2**attempt)

    return results


def step3_complete_multipart(complete_url: str, parts: list[dict]) -> None:
    """POST S3 CompleteMultipartUpload XML to the presigned complete URL."""
    import requests

    part_xml = "\n".join(f"  <Part>\n    <PartNumber>{p['partNumber']}</PartNumber>\n    <ETag>{p['etag']}</ETag>\n  </Part>" for p in parts)
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
        time.sleep(min(interval * (1.5**attempt), 30))
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
    from requests.exceptions import RequestException

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

    url = f"{_API_BASE}/mod-file-update-groups/{group_id}/versions"
    for attempt in range(1, _PART_MAX_RETRIES + 1):
        try:
            resp = requests.post(
                url,
                headers=_api_headers(api_key),
                json=body,
                timeout=60,
            )
            result = _check(resp, "Update mod file")
            file_uid: str = result["data"]["id"]
            return file_uid
        except RequestException as ex:
            if attempt == _PART_MAX_RETRIES:
                raise RuntimeError(f"Update mod file failed after {_PART_MAX_RETRIES} attempts: {ex}") from ex
            print(f"  Attach retry {attempt}/{_PART_MAX_RETRIES}: {ex}")
            time.sleep(2**attempt)
    raise RuntimeError("Update mod file failed.")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def main() -> int:
    ap = argparse.ArgumentParser(description="Upload KitLib zip to Nexus Mods.")
    ap.add_argument("--version", default="", help="Semver, e.g. 0.6.0 (default: KitLib.json)")
    tool_group = ap.add_mutually_exclusive_group()
    tool_group.add_argument(
        "--mcp",
        action="store_true",
        help="Build/package/upload the KitLib.Mcp stdio proxy (make zip-mcp).",
    )
    tool_group.add_argument(
        "--kitlog",
        action="store_true",
        help="Build/package/upload the KitLog CLI (make zip-kitlog).",
    )
    ap.add_argument(
        "--tools-rid",
        default="",
        help="Runtime ID for tool zips (default: TOOLS_RID env or host default).",
    )
    ap.add_argument(
        "--dry-run",
        action="store_true",
        help="Build/package only; skip the actual Nexus upload.",
    )
    args = ap.parse_args()

    # ── resolve version ──────────────────────────────────────────────────────
    version = args.version.strip()
    if not version:
        raw = (_REPO_ROOT / "KitLib.json").read_text(encoding="utf-8")
        manifest = json.loads(raw)
        version = str(manifest["version"])
        print(f"Version auto-detected from KitLib.json: {version}")

    tool: str | None = "mcp" if args.mcp else "kitlog" if args.kitlog else None
    tools_rid = _tools_rid(args.tools_rid)
    zip_path = _zip_path_for(version, tool=tool, tools_rid=tools_rid)
    if tool:
        print(f"Tool runtime: {tools_rid}")

    # ── check credentials ────────────────────────────────────────────────────
    api_key = os.environ.get("NEXUS_API_KEY", "").strip()
    group_id = _resolve_nexus_group_id(tool=tool)
    attach = _nexus_attach_options(tool=tool)

    if not args.dry_run:
        if not api_key:
            print(
                "ERROR: NEXUS_API_KEY environment variable is not set.\n" "       Get your key at https://www.nexusmods.com/settings/api-keys",
                file=sys.stderr,
            )
            return 1
        if not group_id:
            if tool:
                env_name, hint = _tool_env_hint(tool)
            else:
                env_name = "NEXUS_FILE_GROUP_ID"
                hint = "Find it on your mod's Files tab → API Info."
            print(
                f"ERROR: {env_name} environment variable is not set.\n" f"       {hint}",
                file=sys.stderr,
            )
            return 1

    # ── ensure zip exists ────────────────────────────────────────────────────
    if not zip_path.is_file():
        make_target = _make_target_for(tool=tool)
        print(f"Zip not found at {zip_path} - running 'make {make_target}' first...")
        import subprocess

        env = os.environ.copy()
        if tool:
            env["TOOLS_RID"] = tools_rid
        r = subprocess.run(["make", make_target], cwd=_REPO_ROOT, env=env)
        if r.returncode != 0:
            print(f"make {make_target} failed.", file=sys.stderr)
            return 1

    if not zip_path.is_file():
        print(f"ERROR: zip still missing after build: {zip_path}", file=sys.stderr)
        return 1

    # ── build file description ───────────────────────────────────────────────
    if tool == "mcp":
        description = _mcp_description_bbcode(tools_rid)
    elif tool == "kitlog":
        description = _kitlog_description_bbcode(tools_rid)
    else:
        from md_to_nexus import convert_markdown  # noqa: PLC0415

        notes_en = _changelog_section(_REPO_ROOT / "CHANGELOG.md", version)
        notes_zh = _changelog_section(_REPO_ROOT / "CHANGELOG.zh-CN.md", version)
        parts: list[str] = []
        if notes_en:
            parts.append(convert_markdown(notes_en))
        if notes_zh:
            parts.append(convert_markdown(notes_zh))
        description = "\n\n[line]\n\n".join(parts)

    display_name = _nexus_display_name(
        version,
        tool=tool,
        tools_rid=tools_rid,
    )

    if args.dry_run:
        print(f"\n[dry-run] Would upload: {zip_path}")
        print(f"[dry-run]   display_name : {display_name}")
        print(f"[dry-run]   version      : {version}")
        print(f"[dry-run]   group_id     : {group_id or '(missing)'}")
        print(f"[dry-run]   file_category: {attach['file_category']}")
        print(f"[dry-run]   primary_dl   : {attach['primary_mod_manager_download']}")
        print(f"[dry-run]   description  :\n{description[:300] or '(empty)'}")
        return 0

    # ── upload ───────────────────────────────────────────────────────────────
    try:
        import requests  # noqa: F401
    except ImportError:
        print("ERROR: 'requests' package not found. Run: uv sync", file=sys.stderr)
        return 1

    print(f"\nUploading {zip_path.name} to Nexus Mods...")

    print("\n[1/6] Creating multipart upload...")
    upload_info = step1_create_multipart(zip_path, api_key)
    upload_id = upload_info["id"]

    print("\n[2/6] Uploading parts...")
    parts_result = step2_upload_parts(zip_path, upload_info)

    print("\n[3/6] Completing multipart upload...")
    step3_complete_multipart(upload_info["complete_presigned_url"], parts_result)

    print("\n[4/6] Finalising upload...")
    step4_finalise(upload_id, api_key)

    print("\n[5/6] Polling upload state...")
    step5_poll(upload_id, api_key)

    print("\n[6/6] Attaching file to mod page...")
    print(f"  Group {group_id}  category={attach['file_category']}  " f"primary={attach['primary_mod_manager_download']}")
    file_uid = step6_update_mod_file(
        upload_id,
        group_id,
        api_key,
        name=display_name,
        version=version,
        description=description,
        file_category=str(attach["file_category"]),
        archive_existing=bool(attach["archive_existing"]),
        primary_mod_manager_download=bool(attach["primary_mod_manager_download"]),
    )

    print(f"\nDone! File UID: {file_uid}")
    section = "Optional files" if tool else "Main files"
    print(f"      {display_name} is now live on Nexus Mods ({section}).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
