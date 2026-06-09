"""Resolve Godot / MegaDot executable path (Windows, Linux, macOS)."""

from __future__ import annotations

import os
import re
import shutil
import stat
from pathlib import Path

# Windows: shipped .exe names. POSIX: often extensionless or .universal / .x86_64.
_GODOT_WIN_RE = re.compile(r"^(MegaDot|Godot_v4\.5\.1).*mono.*\.exe$", re.IGNORECASE)
_GODOT_POSIX_NAME_RE = re.compile(
    r"^(MegaDot|Godot_v4\.5\.1).*(mono|Mono).*(linux|osx|macos|universal|x86_64)",
    re.IGNORECASE,
)


def _walk_max_depth(root: Path, max_depth: int, predicate) -> Path | None:
    if not root.is_dir():
        return None
    base = root.resolve()
    for dirpath, dirnames, filenames in os.walk(base, topdown=True):
        depth = len(Path(dirpath).relative_to(base).parts)
        if depth > max_depth:
            dirnames[:] = []
            continue
        for name in filenames:
            p = Path(dirpath) / name
            if predicate(p):
                return p
    return None


def _is_executable_file(p: Path) -> bool:
    if not p.is_file():
        return False
    try:
        return bool(p.stat().st_mode & stat.S_IXUSR)
    except OSError:
        return False


def _posix_godot_predicate(p: Path) -> bool:
    if not p.is_file():
        return False
    n = p.name
    s = str(p)
    # macOS .app: .../Godot_mono.app/Contents/MacOS/Godot
    if ".app/" in s and "/MacOS/" in s and "Godot" in n:
        return os.access(p, os.X_OK)
    if _GODOT_POSIX_NAME_RE.match(n):
        return _is_executable_file(p) or os.access(p, os.X_OK)
    if n.lower().startswith("megadot") and (_is_executable_file(p) or os.access(p, os.X_OK)):
        return True
    nl = n.lower()
    if "godot" in nl and "mono" in nl and (_is_executable_file(p) or os.access(p, os.X_OK)):
        return True
    return False


def resolve_godot_path() -> Path | None:
    env = os.environ.get("GODOT_PATH", "").strip()
    if env:
        p = Path(os.path.expandvars(env)).expanduser()
        if p.is_file():
            return p.resolve()

    if os.name == "nt":
        search_roots = [
            Path(r"C:\tools"),
            Path.home(),
            Path(r"C:\dev"),
        ]
        for r in search_roots:
            found = _walk_max_depth(
                r,
                3,
                lambda p: p.is_file() and bool(_GODOT_WIN_RE.match(p.name)),
            )
            if found:
                return found.resolve()
        for name in ("godot.exe", "Godot_mono.exe"):
            w = shutil.which(name)
            if w:
                return Path(w).resolve()
    else:
        for name in ("godot", "Godot", "MegaDot", "Godot_mono"):
            w = shutil.which(name)
            if w:
                wp = Path(w).resolve()
                if wp.is_file():
                    return wp

        home = Path.home()
        search_roots = [
            home / "Applications",
            Path("/Applications"),
            home / "bin",
            home / "tools",
            home / ".local/bin",
            Path("/opt"),
            home / "Library/Application Support/Steam/steamapps/common",
        ]
        for r in search_roots:
            found = _walk_max_depth(r, 4, _posix_godot_predicate)
            if found:
                return found.resolve()

    return None
