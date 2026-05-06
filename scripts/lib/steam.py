"""Resolve Slay the Spire 2 install dir via Steam or STS2_DIR (Windows, Linux, macOS)."""

from __future__ import annotations

import os
import re
import sys
from pathlib import Path

_STS2_COMMON = Path("common") / "Slay the Spire 2"


def _steam_root_windows() -> Path | None:
    if sys.platform != "win32":
        return None
    try:
        import winreg  # type: ignore
    except ImportError:
        return None

    candidates: list[tuple[int, str, str]] = [
        (winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam", "SteamPath"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Valve\Steam", "InstallPath"),
    ]
    for hive, subkey, value_name in candidates:
        try:
            with winreg.OpenKey(hive, subkey) as k:
                p, _ = winreg.QueryValueEx(k, value_name)
                if p and Path(p).exists():
                    return Path(p)
        except OSError:
            continue
    return None


def _steam_install_roots_unix() -> list[Path]:
    """Steam root dirs (parent of steamapps), POSIX."""
    home = Path.home()
    roots: list[Path] = [
        home / ".local/share/Steam",
        home / ".steam/root",
        home / ".steam/steam",
        home / "Library/Application Support/Steam",
        home / ".var/app/com.valvesoftware.Steam/data/Steam",
        home / ".var/app/com.valvesoftware.Steam/.local/share/Steam",
    ]
    snap = home / "snap" / "steam"
    if snap.is_dir():
        common = snap / "common" / ".local" / "share" / "Steam"
        if common.is_dir():
            roots.append(common)
        for rev in snap.iterdir():
            if rev.is_dir() and rev.name not in ("common", "current"):
                p = rev / ".local" / "share" / "Steam"
                if p.is_dir():
                    roots.append(p)
    return [p for p in roots if p.is_dir()]


def _all_steam_install_roots() -> list[Path]:
    roots: list[Path] = []
    w = _steam_root_windows()
    if w:
        roots.append(w)
    if sys.platform != "win32":
        roots.extend(_steam_install_roots_unix())
    return roots


def _parse_libraryfolders_steamapps(steamapps: Path, seen: set[Path], out: list[Path]) -> None:
    libfile = steamapps / "libraryfolders.vdf"
    if not libfile.is_file():
        return
    text = libfile.read_text(encoding="utf-8", errors="replace")
    for m in re.finditer(r'"path"\s+"([^"]+)"', text):
        raw = m.group(1).replace("\\\\", "\\")
        if not raw:
            continue
        p = Path(raw) / "steamapps"
        if p.is_dir():
            rp = p.resolve()
            if rp not in seen:
                seen.add(rp)
                out.append(p)


def _steam_apps_dirs() -> list[Path]:
    seen: set[Path] = set()
    out: list[Path] = []

    for root in _all_steam_install_roots():
        sa = root / "steamapps"
        if not sa.is_dir():
            continue
        rp = sa.resolve()
        if rp not in seen:
            seen.add(rp)
            out.append(sa)

    for sa in list(out):
        _parse_libraryfolders_steamapps(sa, seen, out)

    return out


def _sts2_game_root_valid(game_root: Path) -> bool:
    """True if game root looks like a valid STS2 install on any OS."""
    if not game_root.is_dir():
        return False

    # macOS app bundle layout.
    if (game_root / "SlayTheSpire2.app" / "Contents" / "MacOS" / "Slay the Spire 2").is_file():
        return True

    try:
        for child in game_root.iterdir():
            if child.is_dir() and child.name.startswith("data_sts2_"):
                if (child / "sts2.dll").is_file() or (child / "sts2.dylib").is_file():
                    return True
    except OSError:
        return False
    return False


def resolve_sts2_dir() -> Path | None:
    env = os.environ.get("STS2_DIR", "").strip()
    if env:
        p = Path(os.path.expandvars(env)).expanduser()
        if _sts2_game_root_valid(p):
            return p.resolve()

    for steamapps in _steam_apps_dirs():
        cand = steamapps / _STS2_COMMON
        if _sts2_game_root_valid(cand):
            return cand.resolve()

    return None
