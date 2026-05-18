#!/usr/bin/env python3
"""Convert and merge README files into a single Nexus Mods BBCode file.

Preprocessing applied before conversion:
  - The first `# <title>` heading is stripped (Nexus page already shows the mod title).
  - The language-switcher line (e.g. "**English** | [中文](...)" or "[English](...) | **中文**")
    is stripped.

Usage:
    python scripts/readme_to_nexus.py                          # default: README.md + README.zh-CN.md → assets/readme.nexus.txt
    python scripts/readme_to_nexus.py -o out.txt README.md README.zh-CN.md
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent
_SCRIPTS_DIR = Path(__file__).resolve().parent
if str(_SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPTS_DIR))

from md_to_nexus import convert_markdown  # noqa: E402


_LANG_SWITCH_RE = re.compile(
    r"^\s*(\*\*[^\*]+\*\*\s*\||\[[^\]]+\]\([^)]*\)\s*\|)"
)


def preprocess(text: str, strip_images: bool = False) -> str:
    """Strip the title heading and language-switcher line from a README.

    Also demotes all h2 (##) headings to h3 (###) so they render as [b]
    rather than [heading] in Nexus BBCode.

    If strip_images is True, image lines (![...](...)]) are removed —
    use this for all files after the first to avoid duplicate images.
    """
    lines = text.splitlines()
    out: list[str] = []
    title_stripped = False

    for line in lines:
        stripped = line.strip()

        # Strip the first top-level heading (# Title)
        if not title_stripped and re.match(r"^#\s+\S", stripped):
            title_stripped = True
            continue

        # Strip the language-switcher line
        if _LANG_SWITCH_RE.match(stripped):
            continue

        # Strip image lines from subsequent files
        if strip_images and re.match(r"^!\[", stripped):
            continue

        # Demote ## → ### so h2 renders as [b] not [heading]
        if re.match(r"^##\s", line):
            line = "#" + line

        out.append(line)

    # Remove leading blank lines left after stripping
    while out and not out[0].strip():
        out.pop(0)

    return "\n".join(out)


def build(files: list[Path], separator: str = "\n\n[line]\n\n") -> str:
    parts: list[str] = []
    for idx, path in enumerate(files):
        raw = path.read_text(encoding="utf-8")
        cleaned = preprocess(raw, strip_images=(idx > 0))
        converted = convert_markdown(cleaned)
        if converted:
            parts.append(converted)
    return separator.join(parts)


def main() -> None:
    ap = argparse.ArgumentParser(
        description="Merge README files into Nexus Mods BBCode.",
    )
    ap.add_argument(
        "files",
        nargs="*",
        help="Markdown files to convert (default: README.md README.zh-CN.md)",
    )
    ap.add_argument(
        "-o", "--output",
        default=str(_REPO_ROOT / "assets" / "readme.nexus.txt"),
        help="Output file (default: assets/readme.nexus.txt)",
    )
    ap.add_argument(
        "--separator",
        default="\n\n[line]\n\n",
        help="Separator between merged files (default: [line])",
    )
    args = ap.parse_args()

    paths = [Path(f) for f in args.files] if args.files else [
        _REPO_ROOT / "README.md",
        _REPO_ROOT / "README.zh-CN.md",
    ]

    result = build(paths, separator=args.separator)

    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(result, encoding="utf-8")
    print(f"Written to {out}")


if __name__ == "__main__":
    main()
