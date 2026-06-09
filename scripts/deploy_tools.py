#!/usr/bin/env python3
"""Copy KitLib CLI tools (kitlog, KitLib.Mcp) into game mods/KitLib/tools/."""

from __future__ import annotations

import argparse
import platform
import shutil
import subprocess
import sys
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.steam import read_sts2_dir_from_local_props  # noqa: E402

_REPO = _SCRIPT_DIR.parent
BUNDLE_ID = "KitLib"
TOOLS_SUBDIR = "tools"

TOOL_SPECS = [
    {
        "project": _REPO / "tools" / "KitLog.Cli" / "KitLog.Cli.csproj",
        "publish_folder": "KitLog.Cli",
        "deploy_names": ("kitlog.exe", "kitlog"),
    },
    {
        "project": _REPO / "tools" / "DevMode.Mcp" / "KitLib.Mcp.csproj",
        "publish_folder": "KitLib.Mcp",
        "deploy_names": ("KitLib.Mcp.exe", "KitLib.Mcp"),
    },
]

PUBLISH_FLAGS = [
    "-c",
    "Release",
    "--self-contained",
    "true",
    "-p:PublishSingleFile=true",
]


def _default_tools_rid() -> str:
    system = platform.system()
    machine = platform.machine().lower()
    if system == "Windows":
        return "win-x64"
    if system == "Darwin":
        return "osx-arm64" if machine in ("arm64", "aarch64") else "osx-x64"
    return "linux-arm64" if machine in ("arm64", "aarch64") else "linux-x64"


def _mods_root(game_root: Path) -> Path:
    mac = game_root / "SlayTheSpire2.app" / "Contents" / "MacOS" / "mods"
    if mac.parent.parent.parent.exists():
        return mac
    return game_root / "mods"


def _publish_dir(spec: dict, tools_rid: str) -> Path:
    return _REPO / "build" / "tools" / spec["publish_folder"] / tools_rid / "publish"


def _find_publish_exe(publish_dir: Path, names: tuple[str, ...]) -> Path | None:
    if not publish_dir.is_dir():
        return None
    for name in names:
        candidate = publish_dir / name
        if candidate.is_file():
            return candidate
    return None


def _dotnet_publish(project: Path, tools_rid: str, publish_dir: Path) -> None:
    publish_dir.mkdir(parents=True, exist_ok=True)
    cmd = [
        "dotnet",
        "publish",
        str(project),
        *PUBLISH_FLAGS,
        "-r",
        tools_rid,
        "-o",
        str(publish_dir),
    ]
    print(f"Publishing: {' '.join(cmd)}")
    subprocess.run(cmd, cwd=_REPO, check=True)


def _deploy_tool(
    spec: dict,
    tools_rid: str,
    dst_dir: Path,
    *,
    build: bool,
    build_if_missing: bool,
) -> bool:
    publish_dir = _publish_dir(spec, tools_rid)
    exe = _find_publish_exe(publish_dir, spec["deploy_names"])

    if exe is None and (build or build_if_missing):
        _dotnet_publish(spec["project"], tools_rid, publish_dir)
        exe = _find_publish_exe(publish_dir, spec["deploy_names"])

    if exe is None:
        print(
            f"Note: tool not built, skipped: {spec['publish_folder']} "
            f"(expected under {publish_dir})",
            file=sys.stderr,
        )
        return False

    target_name = exe.name
    target = dst_dir / target_name
    shutil.copy2(exe, target)
    print(f"Deployed tool -> {target}")
    return True


def main() -> int:
    ap = argparse.ArgumentParser(description="Deploy KitLib CLI tools to mods/KitLib/tools/.")
    ap.add_argument("--game-root", type=Path, default=None, help="STS2 install dir (default: local.props Sts2Dir)")
    ap.add_argument("--tools-rid", default=None, help=f"Runtime identifier (default: {_default_tools_rid()})")
    ap.add_argument("--build", action="store_true", help="Always dotnet publish before copy")
    ap.add_argument(
        "--build-if-missing",
        action="store_true",
        help="Publish when publish output is missing (default for sync-full)",
    )
    args = ap.parse_args()

    build = args.build
    build_if_missing = args.build_if_missing or (not build)

    game_root = args.game_root
    if game_root is None:
        game_root = read_sts2_dir_from_local_props(_REPO)
    if game_root is None:
        print("Sts2Dir not set. Run make init or pass --game-root.", file=sys.stderr)
        return 1

    tools_rid = args.tools_rid or _default_tools_rid()
    dst_dir = _mods_root(game_root.resolve()) / BUNDLE_ID / TOOLS_SUBDIR
    dst_dir.mkdir(parents=True, exist_ok=True)

    deployed = 0
    for spec in TOOL_SPECS:
        if _deploy_tool(spec, tools_rid, dst_dir, build=build, build_if_missing=build_if_missing):
            deployed += 1

    if deployed == 0:
        print("No tools deployed.", file=sys.stderr)
        return 1

    print(f"Done: {deployed} tool(s) in {dst_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
