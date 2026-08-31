#!/usr/bin/env python3
"""Convert README files into separate Steam Workshop BBCode drafts (EN + zh-CN).

Outputs (not uploaded by publish_steam.py — paste manually on Steam Workshop):
  assets/readme.steam.en.txt
  assets/readme.steam.zh-CN.txt
  mods/<Product>/assets/readme.steam.en.txt
  mods/<Product>/assets/readme.steam.zh-CN.txt

Source of truth: README.md and README.zh-CN.md next to each product.

Usage:
    python scripts/readme_to_steam.py
    python scripts/readme_to_steam.py --product KitLib
    python scripts/readme_to_steam.py --product KitModPanel
    make readme-steam
    make readme-steam PRODUCT=KitDevTools
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent
_SCRIPTS_DIR = Path(__file__).resolve().parent
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))

from lib.mod_products import PRODUCT_ORDER, PRODUCTS  # noqa: E402
from lib.steam_readme import (  # noqa: E402
    STEAM_DESCRIPTION_MAX,
    build_steam_readme,
    steam_readme_paths,
    validate_steam_readme,
)


def _generate_product(product_id: str) -> None:
    readme_en, readme_zh, out_en, out_zh = steam_readme_paths(_REPO_ROOT, product_id)
    if not readme_en.is_file():
        raise SystemExit(f"Missing {readme_en.relative_to(_REPO_ROOT)}")
    if not readme_zh.is_file():
        raise SystemExit(f"Missing {readme_zh.relative_to(_REPO_ROOT)}")

    for label, source, out in (
        ("en", readme_en, out_en),
        ("zh-CN", readme_zh, out_zh),
    ):
        text = build_steam_readme(source)
        validate_steam_readme(text, label=f"{product_id} {label}")
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(text + "\n", encoding="utf-8")
        print(
            f"Written {out.relative_to(_REPO_ROOT)} "
            f"({len(text)} / {STEAM_DESCRIPTION_MAX} chars)"
        )


def main() -> int:
    ap = argparse.ArgumentParser(description="Generate Steam Workshop BBCode readme drafts.")
    ap.add_argument(
        "--product",
        default="",
        help="One product id (KitLib, KitModPanel, KitDevTools, KitAI). Default: all products.",
    )
    args = ap.parse_args()

    if args.product.strip():
        product_id = args.product.strip()
        if product_id not in PRODUCTS:
            raise SystemExit(f"Unknown product: {product_id}")
        targets = (product_id,)
    else:
        targets = PRODUCT_ORDER

    for product_id in targets:
        _generate_product(product_id)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
