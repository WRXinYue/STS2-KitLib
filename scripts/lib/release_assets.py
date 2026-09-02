"""Release zip naming and paths (beta mod + optional tools)."""

from __future__ import annotations

import json
import os
from pathlib import Path

from lib.mod_products import PRODUCT_ORDER, PRODUCTS

RELEASE_PROFILES = ("beta",)


def tools_rid(cli_value: str = "") -> str:
    if cli_value.strip():
        return cli_value.strip()
    env = os.environ.get("TOOLS_RID", "").strip()
    if env:
        return env
    return "win-x64" if os.name == "nt" else "linux-x64"


def read_product_version(repo_root: Path, product_id: str) -> str:
    if product_id not in PRODUCTS:
        raise ValueError(f"Unknown product: {product_id}")
    manifest = PRODUCTS[product_id].manifest_path
    data = json.loads(manifest.read_text(encoding="utf-8"))
    return str(data["version"])


def product_zip_name(product_id: str, version: str) -> str:
    return f"{product_id}-v{version}.zip"


def product_zip_path(repo_root: Path, product_id: str, version: str) -> Path:
    return repo_root / "build" / product_zip_name(product_id, version)


def mod_zip_name(version: str, profile: str = "beta") -> str:
    _ = profile
    return product_zip_name("KitLib", version)


def mod_zip_path(repo_root: Path, version: str, profile: str = "beta") -> Path:
    _ = profile
    return product_zip_path(repo_root, "KitLib", version)


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


def all_product_zip_paths(repo_root: Path) -> list[Path]:
    return [
        product_zip_path(repo_root, product_id, read_product_version(repo_root, product_id))
        for product_id in PRODUCT_ORDER
    ]


def github_release_assets(repo_root: Path, version: str, rid: str = "") -> list[Path]:
    """Per-product mod zips plus self-contained MCP executable (not tool zip)."""
    resolved_rid = tools_rid(rid)
    return [
        *all_product_zip_paths(repo_root),
        mcp_exe_path(repo_root, resolved_rid),
    ]
