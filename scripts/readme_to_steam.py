#!/usr/bin/env python3
"""Convert README files into separate Steam Workshop BBCode drafts (EN + zh-CN).

Outputs (not uploaded by publish_steam.py — paste manually on Steam Workshop):
  assets/readme.steam.en.txt
  assets/readme.steam.zh-CN.txt

Usage:
    python scripts/readme_to_steam.py
"""

from __future__ import annotations

import sys
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent
_SCRIPTS_DIR = Path(__file__).resolve().parent
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))

from lib.steam_readme import (  # noqa: E402
    STEAM_DESCRIPTION_MAX,
    STEAM_README_EN,
    STEAM_README_ZH,
    build_steam_readme,
    validate_steam_readme,
)

_PROFILES = (
    ("en", _REPO_ROOT / "README.md", STEAM_README_EN),
    ("zh-CN", _REPO_ROOT / "README.zh-CN.md", STEAM_README_ZH),
)


def main() -> int:
    assets = _REPO_ROOT / "assets"
    assets.mkdir(parents=True, exist_ok=True)

    for label, source, filename in _PROFILES:
        if not source.is_file():
            raise SystemExit(f"Missing {source.relative_to(_REPO_ROOT)}")
        text = build_steam_readme(source)
        validate_steam_readme(text, label=label)
        out = assets / filename
        out.write_text(text + "\n", encoding="utf-8")
        print(f"Written {out.relative_to(_REPO_ROOT)} ({len(text)} / {STEAM_DESCRIPTION_MAX} chars)")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
