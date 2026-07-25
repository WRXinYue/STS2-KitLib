"""Build only KitLib mod-bundle projects (excludes MCP, tests, and other tools)."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

_REPO = Path(__file__).resolve().parents[2]
if str(_REPO / "scripts") not in sys.path:
    sys.path.insert(0, str(_REPO / "scripts"))

from lib.sts2_profiles import resolve_profile_dir  # noqa: E402

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
    kitlib_personal_compat: bool = False,
) -> None:
    resolved_dir = sts2_dir
    if sts2_profile and not resolved_dir:
        resolved_dir = str(resolve_profile_dir(sts2_profile, repo_root=_REPO))

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
        if resolved_dir:
            cmd.append(f"-p:Sts2Dir={resolved_dir}")
        if kitlib_personal_compat:
            cmd.append("-p:KitLibPersonalCompat=true")
        subprocess.run(cmd, cwd=_REPO, check=True)
