"""Steam Workshop description from README markdown (parallel to readme.nexus.txt)."""

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

_DEFAULT_SEPARATOR = "\n\n[hr][/hr]\n\n"


def default_readme_paths(repo_root: Path) -> list[Path]:
    root = repo_root.resolve()
    return [root / "README.md", root / "README.zh-CN.md"]


def build_steam_readme(
    files: list[Path],
    *,
    separator: str = _DEFAULT_SEPARATOR,
) -> str:
    parts: list[str] = []
    for idx, path in enumerate(files):
        raw = path.read_text(encoding="utf-8")
        cleaned = preprocess(raw, strip_images=(idx > 0))
        converted = convert_markdown_to_steam(cleaned)
        if converted:
            parts.append(converted)
    return separator.join(parts)


def get_workshop_description(
    repo_root: Path,
    *,
    readme_paths: list[Path] | None = None,
) -> str:
    paths = readme_paths or default_readme_paths(repo_root)
    missing = [p for p in paths if not p.is_file()]
    if missing:
        names = ", ".join(p.name for p in missing)
        raise RuntimeError(f"Missing README file(s) for Steam description: {names}")
    text = build_steam_readme(paths)
    if not text.strip():
        raise RuntimeError("Steam workshop description is empty after README conversion.")
    if len(text) > STEAM_DESCRIPTION_MAX:
        raise RuntimeError(f"Steam workshop description is {len(text)} chars (limit {STEAM_DESCRIPTION_MAX}). " "Shorten README.md / README.zh-CN.md or move detail into docs/.")
    return text
