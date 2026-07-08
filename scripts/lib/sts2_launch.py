"""Shared STS2 direct-launch argument builders (Windows dev / LAN multiplayer)."""

from __future__ import annotations

import subprocess
from pathlib import Path

RENDERING_DRIVER = "vulkan"
DEFAULT_JOIN_CLIENT_ID = 1001
# Official NMainMenu.CheckCommandLineArgs: plain "host" does not call FastHost; use host_standard.
FASTMP_HOST_MODE = "host_standard"


def build_launch_argv(
    exe: Path,
    *,
    fastmp: str | None = None,
    client_id: int = DEFAULT_JOIN_CLIENT_ID,
    extra: list[str] | None = None,
) -> list[str]:
    cmd = [str(exe), "--rendering-driver", RENDERING_DRIVER]
    if extra:
        cmd.extend(extra)
    if fastmp == "host":
        cmd.append(f"--fastmp={FASTMP_HOST_MODE}")
    elif fastmp == "join":
        cmd.extend(["--fastmp=join", f"--clientId={client_id}"])
    elif fastmp is not None:
        raise ValueError(f"Unsupported fastmp mode: {fastmp!r}")
    return cmd


def launch_detached(exe: Path, game_root: Path, argv: list[str]) -> subprocess.Popen[bytes]:
    print(f"Launching: {' '.join(argv)}")
    return subprocess.Popen(
        argv,
        cwd=game_root,
        creationflags=subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP,
        close_fds=True,
    )
