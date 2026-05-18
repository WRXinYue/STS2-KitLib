#!/usr/bin/env python3
"""Convert Markdown to Nexus Mods BBCode.

Usage:
    echo "**bold**" | python scripts/md_to_nexus.py
    python scripts/md_to_nexus.py < README.md
"""

from __future__ import annotations

import re
import sys


def _escape_html(text: str) -> str:
    """Escape bare HTML angle brackets so Nexus doesn't eat them."""
    return text.replace("<", "&lt;").replace(">", "&gt;")


def convert_inline(text: str) -> str:
    # Escape HTML angle brackets first (before any other substitution)
    text = _escape_html(text)
    text = re.sub(r"~~(.*?)~~", r"[strike]\1[/strike]", text)
    text = re.sub(r"\*\*(.+?)\*\*", r"[b]\1[/b]", text)
    text = re.sub(r"__(.+?)__", r"[b]\1[/b]", text)
    text = re.sub(r"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", r"[i]\1[/i]", text)
    text = re.sub(r"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", r"[i]\1[/i]", text)
    # Inline code: Nexus has no inline code tag — use [i] for visual distinction
    text = re.sub(r"`([^`]+)`", r"[i]\1[/i]", text)
    text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r"[url=\2]\1[/url]", text)
    return text


def convert_markdown(text: str) -> str:
    lines = text.splitlines()
    output: list[str] = []
    in_code = False
    in_list = False
    in_olist = False
    in_quote = False

    def close_lists() -> None:
        nonlocal in_list, in_olist
        if in_list:
            output.append("[/list]")
            in_list = False
        if in_olist:
            output.append("[/list]")
            in_olist = False

    def close_quote() -> None:
        nonlocal in_quote
        if in_quote:
            output.append("[/quote]")
            in_quote = False

    for raw in lines:
        line = raw.rstrip("\n")
        stripped = line.strip()

        # ── fenced code block ────────────────────────────────────────────────
        if stripped.startswith("```"):
            close_lists()
            close_quote()
            if in_code:
                output.append("[/code]")
                in_code = False
            else:
                output.append("[code]")
                in_code = True
            continue

        if in_code:
            output.append(line)
            continue

        # ── horizontal rule → [line] ─────────────────────────────────────────
        if re.match(r"^\s*[-*_]\s*[-*_]\s*[-*_]\s*$", stripped):
            close_lists()
            close_quote()
            output.append("[line]")
            continue

        # ── blockquote ───────────────────────────────────────────────────────
        if stripped.startswith(">"):
            close_lists()
            if not in_quote:
                output.append("[quote]")
                in_quote = True
            content = stripped.lstrip(">").lstrip()
            output.append(convert_inline(content))
            continue
        else:
            if in_quote and stripped == "":
                close_quote()

        # ── blank line ───────────────────────────────────────────────────────
        if stripped == "":
            close_lists()
            close_quote()
            output.append("")
            continue

        # ── headings: h1/h2 → [heading], h3+ → [b] ──────────────────────────
        m = re.match(r"^(#{1,6})\s+(.+)$", stripped)
        if m:
            close_lists()
            close_quote()
            level = len(m.group(1))
            title = convert_inline(m.group(2))
            if level <= 2:
                output.append(f"[heading]{title}[/heading]")
            else:
                output.append(f"[b]{title}[/b]")
            continue

        # ── ordered list → [list=1][*]...[/list] ────────────────────────────
        m = re.match(r"^\s*(\d+)\.\s+(.+)$", line)
        if m:
            close_quote()
            if not in_olist:
                close_lists()
                output.append("[list=1]")
                in_olist = True
            output.append(f"[*]{convert_inline(m.group(2))}")
            continue

        # ── unordered list → [list][*]...[/list] ────────────────────────────
        m = re.match(r"^\s*[-*+]\s+(.+)$", line)
        if m:
            close_quote()
            if not in_list:
                close_lists()
                output.append("[list]")
                in_list = True
            output.append(f"[*]{convert_inline(m.group(1))}")
            continue

        close_lists()
        close_quote()
        output.append(convert_inline(line))

    close_lists()
    close_quote()
    if in_code:
        output.append("[/code]")

    result = "\n".join(output).strip()
    result = re.sub(r"\n{3,}", "\n\n", result)
    return result


def main() -> None:
    text = sys.stdin.read()
    if text:
        sys.stdout.write(convert_markdown(text))


if __name__ == "__main__":
    main()
