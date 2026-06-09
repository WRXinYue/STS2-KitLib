#!/usr/bin/env python3
"""Upload the mod zip to Nexus Mods using the v3 multipart upload API.

Environment variables (required):
    NEXUS_API_KEY              - Your personal API key from nexusmods.com/settings/api-keys
    NEXUS_FILE_GROUP_ID        - Main file group ID (stable/public build; Files tab → API Info)
    NEXUS_FILE_GROUP_ID_BETA   - Optional file group ID for STS2 Steam beta builds (separate line)
    NEXUS_FILE_GROUP_ID_MCP    - Optional file group ID for KitLib.Mcp stdio proxy (separate line)

Usage:
    python scripts/publish_nexus.py [--version X.Y.Z] [--beta] [--mcp] [--dry-run]
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent


def _sts2_beta_version(raw: str) -> str:
    return raw.strip().lstrip("v") or "0.105.1"


def _resolve_sts2_beta_version(cli_value: str) -> str:
    if cli_value.strip():
        return _sts2_beta_version(cli_value)
    return _sts2_beta_version(os.environ.get("STS2_GAME_BETA_VERSION", "0.105.1"))


def _zip_path(version: str, *, beta: bool, sts2_beta_version: str) -> Path:
    if beta:
        return _REPO_ROOT / "build" / f"KitLib-v{version}-sts2beta-v{sts2_beta_version}.zip"
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


def _mcp_description_bbcode(tools_rid: str) -> str:
    exe = "KitLib.Mcp.exe" if tools_rid.startswith("win") else "KitLib.Mcp"
    return (
        "[b]DevMode MCP stdio proxy[/b] (optional dev tool)\n\n"
        "Connect MCP clients (Cursor, Claude Desktop, etc.) to a running Slay the Spire 2 session "
        "with DevMode loaded.\n\n"
        f"Extract [code]{exe}[/code] and point your MCP client [code]devmode[/code] entry at it with "
        "[code]--port 9877[/code]. Full setup: GitHub README → MCP section.\n\n"
        "Requires the DevMode mod installed and the game running."
    )


def _beta_notice_bbcode(sts2_beta_version: str) -> str:
    return (
        f"[b]Requires Slay the Spire 2 Steam beta branch v{sts2_beta_version}.[/b] "
        "Do not use on the public/stable game build.\n"
        "If you are on the Release branch, download the Main file instead.\n\n"
    )


def _resolve_nexus_group_id(*, beta: bool, mcp: bool) -> str:
    if mcp:
        return os.environ.get("NEXUS_FILE_GROUP_ID_MCP", "").strip()
    if beta:
        return os.environ.get("NEXUS_FILE_GROUP_ID_BETA", "").strip()
    return os.environ.get("NEXUS_FILE_GROUP_ID", "").strip()


def _nexus_display_name(
    version: str,
    *,
    beta: bool,
    mcp: bool,
    sts2_beta_version: str,
    tools_rid: str,
) -> str:
    if mcp:
        return f"KitLib.Mcp v{version} ({tools_rid})"
    if beta:
        safe = sts2_beta_version.strip().lstrip("v")
        return f"KitLib.Compat.sts2beta-v{safe}"
    return f"KitLib v{version}"


def _nexus_attach_options(*, beta: bool, mcp: bool) -> dict[str, object]:
    if beta or mcp:
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
                    raise RuntimeError(f"Part {part_number} upload failed after {_PART_MAX_RETRIES} attempts: " f"HTTP {resp.status_code}")
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
    ap.add_argument("--version", default="", help="Semver, e.g. 0.6.0 (default: KitLib.json)")
    ap.add_argument(
        "--beta",
        action="store_true",
        help="Build/package/upload the STS2 Steam beta build (make zip-beta).",
    )
    ap.add_argument(
        "--mcp",
        action="store_true",
        help="Build/package/upload the KitLib.Mcp stdio proxy (make zip-mcp).",
    )
    ap.add_argument(
        "--tools-rid",
        default="",
        help="Runtime ID for MCP zip (default: TOOLS_RID env or host default).",
    )
    ap.add_argument(
        "--sts2-beta-version",
        default="",
        help="STS2 game beta version label, e.g. 0.105.1 (default: STS2_GAME_BETA_VERSION env or 0.105.1).",
    )
    ap.add_argument(
        "--dry-run",
        action="store_true",
        help="Build/package only; skip the actual Nexus upload.",
    )
    args = ap.parse_args()

    if args.beta and args.mcp:
        print("ERROR: --beta and --mcp are mutually exclusive.", file=sys.stderr)
        return 1

    # ── resolve version ──────────────────────────────────────────────────────
    version = args.version.strip()
    if not version:
        raw = (_REPO_ROOT / "KitLib.json").read_text(encoding="utf-8")
        manifest = json.loads(raw)
        version = str(manifest["version"])
        print(f"Version auto-detected from KitLib.json: {version}")

    sts2_beta_version = _resolve_sts2_beta_version(args.sts2_beta_version)
    tools_rid = _tools_rid(args.tools_rid)
    if args.mcp:
        zip_path = _mcp_zip_path(version, tools_rid)
        print(f"MCP runtime: {tools_rid}")
    else:
        zip_path = _zip_path(version, beta=args.beta, sts2_beta_version=sts2_beta_version)
    if args.beta:
        print(f"STS2 Steam beta branch game version: v{sts2_beta_version}")

    # ── check credentials ────────────────────────────────────────────────────
    api_key = os.environ.get("NEXUS_API_KEY", "").strip()
    group_id = _resolve_nexus_group_id(beta=args.beta, mcp=args.mcp)
    attach = _nexus_attach_options(beta=args.beta, mcp=args.mcp)

    if not args.dry_run:
        if not api_key:
            print(
                "ERROR: NEXUS_API_KEY environment variable is not set.\n" "       Get your key at https://www.nexusmods.com/settings/api-keys",
                file=sys.stderr,
            )
            return 1
        if not group_id:
            if args.mcp:
                env_name = "NEXUS_FILE_GROUP_ID_MCP"
                hint = "Create an Optional file for KitLib.Mcp on your mod page first, " "then copy its group ID from API Info."
            elif args.beta:
                env_name = "NEXUS_FILE_GROUP_ID_BETA"
                hint = "Create an Optional file on your mod page first, then copy its group ID from API Info."
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
        if args.mcp:
            make_target = "zip-mcp"
        else:
            make_target = "zip-beta" if args.beta else "zip"
        print(f"Zip not found at {zip_path} — running 'make {make_target}' first…")
        import subprocess

        env = os.environ.copy()
        if args.mcp:
            env["TOOLS_RID"] = tools_rid
        r = subprocess.run(["make", make_target], cwd=_REPO_ROOT, env=env)
        if r.returncode != 0:
            print(f"make {make_target} failed.", file=sys.stderr)
            return 1

    if not zip_path.is_file():
        print(f"ERROR: zip still missing after build: {zip_path}", file=sys.stderr)
        return 1

    # ── build file description ───────────────────────────────────────────────
    if args.beta:
        description = _beta_notice_bbcode(sts2_beta_version)
    elif args.mcp:
        description = _mcp_description_bbcode(tools_rid)
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
        beta=args.beta,
        mcp=args.mcp,
        sts2_beta_version=sts2_beta_version,
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
    section = "Optional files" if (args.beta or args.mcp) else "Main files"
    print(f"      {display_name} is now live on Nexus Mods ({section}).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
