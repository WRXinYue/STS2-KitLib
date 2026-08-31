"""Steam Workshop listing description helpers (manual drafts; not synced on publish)."""

from __future__ import annotations

import sys
from pathlib import Path

_SCRIPTS_DIR = Path(__file__).resolve().parent.parent
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))

from lib.steam_changelog import convert_markdown_to_steam  # noqa: E402
from readme_to_nexus import preprocess  # noqa: E402

# Steam ISteamUGC::SetItemDescription limit (k_cchPublishedDocumentDescriptionMax).
STEAM_DESCRIPTION_MAX = 8000

STEAM_README_EN = "readme.steam.en.txt"
STEAM_README_ZH = "readme.steam.zh-CN.txt"


def steam_readme_paths(repo_root: Path, product_id: str) -> tuple[Path, Path, Path, Path]:
    """Return (readme_en, readme_zh, out_en, out_zh) for a product."""
    if product_id == "KitLib":
        base = repo_root
        assets = repo_root / "assets"
    else:
        base = repo_root / "mods" / product_id
        assets = base / "assets"
    return (
        base / "README.md",
        base / "README.zh-CN.md",
        assets / STEAM_README_EN,
        assets / STEAM_README_ZH,
    )


def build_steam_readme(path: Path) -> str:
    raw = path.read_text(encoding="utf-8")
    cleaned = preprocess(raw, strip_images=False)
    converted = convert_markdown_to_steam(cleaned)
    if not converted.strip():
        raise ValueError(f"Steam readme is empty after conversion: {path}")
    return converted.strip()


def validate_steam_readme(text: str, *, label: str) -> None:
    if len(text) > STEAM_DESCRIPTION_MAX:
        raise ValueError(f"{label} is {len(text)} chars (Steam limit {STEAM_DESCRIPTION_MAX}). " "Shorten the README or move detail into docs/.")
