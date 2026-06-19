"""Steam Workshop changeNote formatting (FoxHimeMod workshop_common changenote subset)."""

from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path

_SCRIPTS_DIR = Path(__file__).resolve().parent.parent

_DASHES = ("— " * 17).rstrip()
_EN_SEP = f"[b]{_DASHES} EN {_DASHES}[/b]"
_ZH_SEP = f"[b]{_DASHES} 中文 {_DASHES}[/b]"

_ZH_SECTIONS = {
    "Added": "新增",
    "Changed": "变更",
    "Fixed": "修复",
    "Removed": "移除",
    "Deprecated": "弃用",
    "Security": "安全",
}


def read_kitlib_version(repo_root: Path) -> str:
    manifest = repo_root / "KitLib.json"
    if not manifest.is_file():
        return ""
    try:
        data = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return ""
    return str(data.get("version") or "").strip()


def extract_changelog_section(changelog_path: Path, *, unreleased: bool = False) -> str:
    if not changelog_path.is_file():
        return ""
    lines = changelog_path.read_text(encoding="utf-8", errors="replace").splitlines()
    header_index = -1
    for idx, line in enumerate(lines):
        if unreleased:
            if re.match(r"^\s*##\s*\[Unreleased\]", line):
                header_index = idx
                break
        elif re.match(r"^\s*##\s*\[(?!Unreleased\])", line):
            header_index = idx
            break
    if header_index < 0:
        return ""
    end = len(lines)
    for j in range(header_index + 1, len(lines)):
        if re.match(r"^\s*##\s+", lines[j]):
            end = j
            break
    segment = [ln.rstrip() for ln in lines[header_index + 1 : end]]
    return "\n".join(segment).strip()


def _translate_zh_sections(text: str) -> str:
    for en, zh in _ZH_SECTIONS.items():
        pattern = r"^(#{1,3}\s+)" + en + r"(\s*)$"
        text = re.sub(
            pattern,
            lambda m, z=zh: m.group(1) + z + m.group(2),
            text,
            flags=re.MULTILINE,
        )
    return text


def build_change_note(version: str, en_text: str, zh_text: str) -> str:
    zh = _translate_zh_sections(zh_text) if zh_text else ""
    parts: list[str] = []
    if version:
        parts.append("[b] v" + version + " [/b]")
    if en_text:
        parts.append(_EN_SEP + "\n\n" + en_text)
    if zh:
        parts.append(_ZH_SEP + "\n\n" + zh)
    if not en_text and not zh:
        return ""
    return "\n\n".join(parts).strip()


def convert_markdown_to_steam(text: str) -> str:
    converter = _SCRIPTS_DIR / "markdown-to-steam.py"
    if not converter.is_file():
        raise RuntimeError(f"Markdown converter not found: {converter}")
    result = subprocess.run(
        [sys.executable, str(converter)],
        input=text,
        text=True,
        encoding="utf-8",
        capture_output=True,
        check=True,
    )
    return result.stdout if result.stdout else text


def format_change_note(text: str) -> str:
    text = convert_markdown_to_steam(text)
    text = re.sub(r"\[h[123]\](.*?)\[/h[123]\]", r"[b][ \1 ][/b]", text)
    text = re.sub(r"\[/?code\]", "", text)
    text = re.sub(r"\[/?(?:o)?list\]", "", text)
    text = text.replace("[*]", "[b]•[/b] ")
    text = re.sub(r"\n{3,}", "\n\n", text).strip()
    return text


def get_change_note(
    repo_root: Path,
    *,
    changelog_en: Path | None = None,
    changelog_zh: Path | None = None,
    prefer_unreleased: bool = False,
) -> str:
    en_path = changelog_en or (repo_root / "CHANGELOG.md")
    zh_path = changelog_zh or (repo_root / "CHANGELOG.zh-CN.md")
    version = read_kitlib_version(repo_root)

    if prefer_unreleased:
        en = extract_changelog_section(en_path, unreleased=True)
        zh = extract_changelog_section(zh_path, unreleased=True)
    else:
        en = extract_changelog_section(en_path, unreleased=False)
        zh = extract_changelog_section(zh_path, unreleased=False)
        if not en and not zh:
            en = extract_changelog_section(en_path, unreleased=True)
            zh = extract_changelog_section(zh_path, unreleased=True)

    raw = build_change_note(version, en, zh)
    if not raw:
        return ""
    return format_change_note(raw)
