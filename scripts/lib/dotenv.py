"""Minimal .env loader (KEY=VALUE into os.environ)."""

from __future__ import annotations

import os
import re
from pathlib import Path


def load_dotenv(path: Path | None) -> None:
    if not path or not path.is_file():
        return
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in raw:
            continue
        key, _, rest = raw.partition("=")
        key = key.strip()
        if not key:
            continue
        value = rest.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
            value = value[1:-1]
        os.environ[key] = value


def load_release_config(repo_root: Path) -> None:
    """Load release.env then .env (.env overrides committed defaults)."""
    root = repo_root.resolve()
    load_dotenv(root / "release.env")
    load_dotenv(root / ".env")


def upsert_env_key(path: Path, key: str, value: str) -> bool:
    """Set KEY=VALUE in an env file, preserving comments and other keys."""
    key = key.strip()
    if not key:
        raise ValueError("key must be non-empty")

    lines: list[str] = []
    if path.is_file():
        lines = path.read_text(encoding="utf-8").splitlines()

    pattern = re.compile(rf"^\s*{re.escape(key)}\s*=")
    replaced = False
    out: list[str] = []
    for line in lines:
        if pattern.match(line):
            out.append(f"{key}={value}")
            replaced = True
        else:
            out.append(line)

    if not replaced:
        if out and out[-1].strip():
            out.append("")
        out.append(f"{key}={value}")

    new_text = "\n".join(out) + "\n"
    old_text = "\n".join(lines) + ("\n" if lines else "")
    if new_text == old_text:
        return False

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(new_text, encoding="utf-8")
    return True
