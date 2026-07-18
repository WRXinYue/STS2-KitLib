"""Release zip naming and paths (beta mod + optional tools)."""

from __future__ import annotations

import os
from pathlib import Path

RELEASE_PROFILES = ("beta",)


def tools_rid(cli_value: str = "") -> str:
    if cli_value.strip():
        return cli_value.strip()
    env = os.environ.get("TOOLS_RID", "").strip()
    if env:
        return env
    return "win-x64" if os.name == "nt" else "linux-x64"


def mod_zip_name(version: str, profile: str = "beta") -> str:
    _ = profile
    return f"KitLib-v{version}.zip"


def mod_zip_path(repo_root: Path, version: str, profile: str = "beta") -> Path:
    _ = profile
    return repo_root / "build" / mod_zip_name(version)


def mcp_zip_name(version: str, rid: str) -> str:
    return f"KitLib.Mcp-v{version}-{rid}.zip"


def mcp_zip_path(repo_root: Path, version: str, rid: str) -> Path:
    return repo_root / "build" / mcp_zip_name(version, rid)


def _tool_publish_dir(repo_root: Path, rid: str) -> Path:
    return repo_root / "build" / "tools" / "KitLib.Mcp" / rid / "publish"


def mcp_exe_path(repo_root: Path, rid: str) -> Path:
    resolved = tools_rid(rid)
    name = "KitLib.Mcp.exe" if resolved.startswith("win") else "KitLib.Mcp"
    return _tool_publish_dir(repo_root, resolved) / name


def github_release_assets(repo_root: Path, version: str, rid: str = "") -> list[Path]:
    """Mod zip plus self-contained MCP executable (not tool zip)."""
    resolved_rid = tools_rid(rid)
    return [
        mod_zip_path(repo_root, version),
        mcp_exe_path(repo_root, resolved_rid),
    ]
