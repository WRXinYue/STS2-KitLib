"""Build only KitLib mod-bundle projects (excludes MCP, tests, and other tools)."""

from __future__ import annotations

import subprocess
from pathlib import Path

_REPO = Path(__file__).resolve().parents[2]

# Keep in sync with package_modules.BUNDLE_DLLS + host entry/core/loader deps.
MOD_BUNDLE_PROJECTS = [
    "src/KitLib.Abstractions/KitLib.Abstractions.csproj",
    "src/KitLib.Core/KitLib.Core.csproj",
    "src/KitLib.Loader/KitLib.Loader.csproj",
    "src/KitLib.Modules.User/KitLib.User.csproj",
    "src/KitLib.Modules.AI/KitLib.AI.csproj",
    "src/KitLib.Modules.ModPanel/KitLib.ModPanel.csproj",
    "src/KitLib.Modules.Panel/KitLib.Panel.csproj",
    "src/KitLib.Modules.Cheat/KitLib.Cheat.csproj",
    "src/KitLib.Modules.Dev/KitLib.Dev.csproj",
]


def build_bundle(
    *,
    configuration: str = "Debug",
    sts2_profile: str | None = None,
    sts2_dir: str | None = None,
) -> None:
    for project in MOD_BUNDLE_PROJECTS:
        cmd = [
            "dotnet",
            "build",
            str(_REPO / project),
            "-c",
            configuration,
            "-v",
            "minimal",
        ]
        if sts2_profile:
            cmd.append(f"-p:Sts2Profile={sts2_profile}")
        if sts2_dir:
            cmd.append(f"-p:Sts2Dir={sts2_dir}")
        subprocess.run(cmd, cwd=_REPO, check=True)
