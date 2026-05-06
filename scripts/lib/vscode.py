"""Write VS Code tasks.json / launch.json and STS2 debug helper files."""

from __future__ import annotations

import json
import sys
import warnings
from pathlib import Path


def setup_debug_launch(sts2_dir: Path) -> None:
    if sys.platform != "win32":
        return

    app_id = sts2_dir / "steam_appid.txt"
    debug_bat = sts2_dir / "launch_debug.bat"
    try:
        app_id.write_text("2868840", encoding="ascii")
        debug_bat.write_bytes(b'@echo off\r\n"%~dp0SlayTheSpire2.exe" --log --rendering-driver opengl3 %*')
        print(f"Generated {debug_bat}")
    except OSError as e:
        warnings.warn(f"Could not write debug files to STS2 dir (try running as admin): {e}", stacklevel=1)


def write_vscode_files(root: Path, sts2_dir: Path) -> None:
    vscode_dir = root / ".vscode"
    vscode_dir.mkdir(parents=True, exist_ok=True)

    tasks = {
        "version": "2.0.0",
        "tasks": [
            {
                "label": "compile-dll",
                "type": "shell",
                "command": "dotnet build DevMode.sln /p:DeployToGame=true",
                "group": {"kind": "build", "isDefault": True},
                "presentation": {"clear": True, "panel": "shared"},
                "problemMatcher": "$msCompile",
            },
            {
                "label": "export-pck",
                "type": "shell",
                "command": "dotnet",
                "args": ["publish", "/p:DeployToGame=true", "DevMode.csproj"],
                "group": "build",
                "presentation": {"clear": True, "panel": "shared"},
                "problemMatcher": "$msCompile",
            },
            {
                "label": "deploy",
                "type": "shell",
                "command": "dotnet",
                "args": [
                    "msbuild",
                    "DevMode.csproj",
                    "-t:DeployRepoBuildToMods",
                    "-p:DeployFromRepoBuild=true",
                ],
                "group": "build",
                "presentation": {"clear": True, "panel": "shared"},
                "problemMatcher": "$msCompile",
            },
            {
                "label": "build",
                "type": "shell",
                "command": "dotnet",
                "args": ["publish", "DevMode.csproj"],
                "group": "build",
                "presentation": {"clear": True, "panel": "shared"},
                "problemMatcher": "$msCompile",
            },
            {
                "label": "sync",
                "dependsOn": ["build", "deploy"],
                "dependsOrder": "sequence",
            },
        ],
    }

    if sys.platform == "win32":
        debug_bat = str((sts2_dir / "launch_debug.bat").resolve())
        tasks["tasks"].append(
            {
                "label": "launch-sts2",
                "type": "shell",
                "command": "cmd.exe",
                "args": ["/c", debug_bat],
                "presentation": {
                    "reveal": "always",
                    "panel": "dedicated",
                    "clear": True,
                },
                "isBackground": True,
            }
        )
    elif sys.platform == "darwin":
        tasks["tasks"].append(
            {
                "label": "launch-sts2",
                "type": "shell",
                "command": "open",
                "args": ["steam://run/2868840"],
                "presentation": {
                    "reveal": "always",
                    "panel": "dedicated",
                    "clear": True,
                },
                "isBackground": True,
            }
        )
    else:
        tasks["tasks"].append(
            {
                "label": "launch-sts2",
                "type": "shell",
                "command": "xdg-open",
                "args": ["steam://run/2868840"],
                "presentation": {
                    "reveal": "always",
                    "panel": "dedicated",
                    "clear": True,
                },
                "isBackground": True,
            }
        )

    tasks["tasks"].append(
        {
            "label": "sync-launch",
            "dependsOn": ["sync", "launch-sts2"],
            "dependsOrder": "sequence",
        }
    )

    cfg = {
        "name": "STS2: sync -> launch -> attach",
        "type": "coreclr",
        "request": "attach",
        "processName": "SlayTheSpire2",
        "preLaunchTask": "sync-launch",
    }
    if sys.platform == "darwin":
        cfg["osx"] = {"processName": "Slay the Spire 2"}

    launch = {"version": "0.2.0", "configurations": [cfg]}

    text_tasks = json.dumps(tasks, indent=2) + "\n"
    text_launch = json.dumps(launch, indent=2) + "\n"
    (vscode_dir / "tasks.json").write_text(text_tasks, encoding="utf-8")
    (vscode_dir / "launch.json").write_text(text_launch, encoding="utf-8")
    print(f"Generated {vscode_dir / 'tasks.json'}")
    print(f"Generated {vscode_dir / 'launch.json'}")
