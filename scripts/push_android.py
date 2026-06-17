#!/usr/bin/env python3
"""Push KitLib mod bundle to Android via adb.

Targets:
  - launcher: /storage/emulated/0/StS2LauncherMM/Mods/<ModId> (default)
  - game:     /data/data/<pkg>/files/mods/<ModId> via `run-as` (direct install / sandbox)
"""

from __future__ import annotations

import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
_REPO_ROOT = _SCRIPT_DIR.parent

DEFAULT_GAME_PACKAGE = "com.megacrit.sts2"
LAUNCHER_PACKAGE = "com.game.sts2launcher.modmanager"
LAUNCHER_MODS_ROOT = "/storage/emulated/0/StS2LauncherMM/Mods"

KITLIB_MOD_ID = "KitLib"
RITSU_MOD_ID = "STS2-RitsuLib"
RITSU_PKG_PREFIX = "sts2.ritsulib"

_SKIP_SUFFIXES = {".pdb"}
_SKIP_NAME_SUFFIXES = (".deps.json", ".runtimeconfig.json")


@dataclass(frozen=True)
class PushTarget:
    name: str
    restart_package: str
    mods_root_display: str
    use_run_as: bool

    def remote_mod_dir(self, mod_id: str) -> str:
        if self.use_run_as:
            return f"files/mods/{mod_id}"
        return f"{LAUNCHER_MODS_ROOT}/{mod_id}"


PUSH_TARGETS: dict[str, PushTarget] = {
    "launcher": PushTarget(
        name="launcher",
        restart_package=LAUNCHER_PACKAGE,
        mods_root_display=f"{LAUNCHER_MODS_ROOT}/",
        use_run_as=False,
    ),
    "game": PushTarget(
        name="game",
        restart_package=DEFAULT_GAME_PACKAGE,
        mods_root_display=f"user://mods -> /data/data/{DEFAULT_GAME_PACKAGE}/files/mods/",
        use_run_as=True,
    ),
}


def _fail(message: str) -> None:
    print(message, file=sys.stderr)
    raise SystemExit(1)


def _resolve_push_target(name: str) -> PushTarget:
    target = PUSH_TARGETS.get(name)
    if target is None:
        known = ", ".join(PUSH_TARGETS)
        _fail(f"Unknown push target {name!r}. Choose from: {known}")
    return target


def _resolve_adb() -> Path:
    env_adb = os.environ.get("ADB", "").strip()
    if env_adb:
        path = Path(env_adb)
        if path.is_file():
            return path

    found = shutil.which("adb")
    if found:
        return Path(found)

    android_home = os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT")
    if android_home:
        for name in ("adb.exe", "adb"):
            candidate = Path(android_home) / "platform-tools" / name
            if candidate.is_file():
                return candidate

    if sys.platform == "win32":
        local = (
            Path(os.environ.get("LOCALAPPDATA", ""))
            / "Android"
            / "Sdk"
            / "platform-tools"
            / "adb.exe"
        )
        if local.is_file():
            return local

    _fail(
        "adb not found. Install Android SDK Platform-Tools or set ADB / ANDROID_HOME.\n"
        "  Example: %LOCALAPPDATA%\\Android\\Sdk\\platform-tools\\adb.exe"
    )


class Adb:
    def __init__(self, exe: Path, serial: str | None = None) -> None:
        self.exe = exe
        self.serial = serial

    def run(self, *args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
        cmd = [str(self.exe)]
        if self.serial:
            cmd.extend(["-s", self.serial])
        cmd.extend(args)
        return subprocess.run(cmd, check=check, text=True, capture_output=True)

    def check_output(self, *args: str) -> str:
        return self.run(*args).stdout

    def shell(self, command: str, *, check: bool = True) -> subprocess.CompletedProcess[str]:
        return self.run("shell", command, check=check)


def _list_devices(adb: Adb) -> list[str]:
    lines = adb.check_output("devices").splitlines()[1:]
    devices: list[str] = []
    for line in lines:
        line = line.strip()
        if not line:
            continue
        parts = line.split()
        if len(parts) >= 2 and parts[1] == "device":
            devices.append(parts[0])
    return devices


def _read_pkg_ref(repo_root: Path, pkg_prefix: str) -> tuple[str | None, str | None]:
    csproj = repo_root / "src" / "KitLib.Core" / "KitLib.Core.csproj"
    try:
        text = csproj.read_text(encoding="utf-8")
    except OSError:
        return None, None
    for m in re.finditer(r"<PackageReference\b([^>]+)>", text, re.IGNORECASE):
        attrs = m.group(1)
        inc = re.search(r'\bInclude="([^"]*)"', attrs, re.IGNORECASE)
        ver = re.search(r'\bVersion="([^"]+)"', attrs, re.IGNORECASE)
        if inc and ver and inc.group(1).lower().startswith(pkg_prefix):
            return inc.group(1).lower(), ver.group(1)
    return None, None


def _nuget_package_roots(repo_root: Path) -> list[Path]:
    roots: list[Path] = []
    env = os.environ.get("NUGET_PACKAGES")
    if env:
        roots.append(Path(env))
    repo_packages = repo_root / "packages"
    if repo_packages.is_dir():
        roots.append(repo_packages)
    global_packages = Path.home() / ".nuget" / "packages"
    if global_packages.is_dir():
        roots.append(global_packages)
    return roots


def _ritsulib_source_dir(repo_root: Path) -> Path | None:
    pkg_id, version = _read_pkg_ref(repo_root, RITSU_PKG_PREFIX)
    if not pkg_id or not version:
        return None
    for root in _nuget_package_roots(repo_root):
        base = root / pkg_id / version
        if base.is_dir():
            return base
    return None


def _validate_kitlib_build(repo_root: Path) -> Path:
    bundle = repo_root / "build" / KITLIB_MOD_ID
    if not bundle.is_dir():
        _fail(f"Build folder missing: {bundle}\nRun `make build` first.")
    if not (bundle / "KitLib.dll").is_file():
        _fail(f"Missing KitLib.dll in {bundle}\nRun `make build` first.")
    if not (bundle / "mod_manifest.json").is_file():
        _fail(f"Missing mod_manifest.json in {bundle}\nRun `make build` first.")
    return bundle


def _stage_tree(src_dir: Path, *, prefix: str) -> Path:
    staging = Path(tempfile.mkdtemp(prefix=prefix))
    for item in src_dir.rglob("*"):
        if item.is_dir():
            continue
        name_lower = item.name.lower()
        if item.suffix.lower() in _SKIP_SUFFIXES:
            continue
        if any(name_lower.endswith(s) for s in _SKIP_NAME_SUFFIXES):
            continue
        rel = item.relative_to(src_dir)
        dst = staging / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(item, dst)
    return staging


def _stage_ritsulib(repo_root: Path) -> Path | None:
    base = _ritsulib_source_dir(repo_root)
    if base is None:
        return None

    lib_dir = base / "lib" / "net9.0"
    content_dir = base / "content"  # project uses /content/mod_manifest.json
    dll = lib_dir / "STS2-RitsuLib.dll"
    manifest = content_dir / "mod_manifest.json"
    if not dll.is_file() or not manifest.is_file():
        return None

    staging = Path(tempfile.mkdtemp(prefix="sts2-push-ritsulib-"))
    shutil.copy2(dll, staging / "STS2-RitsuLib.dll")
    shutil.copy2(manifest, staging / "mod_manifest.json")
    rc = lib_dir / "STS2-RitsuLib.runtimeconfig.json"
    if rc.is_file():
        shutil.copy2(rc, staging / "STS2-RitsuLib.runtimeconfig.json")
    return staging


def _push_mod_dir(
    adb: Adb,
    target: PushTarget,
    game_package: str,
    local_dir: Path,
    remote_mod_id: str,
) -> None:
    tmp = f"/data/local/tmp/sts2_push_{remote_mod_id}"
    remote_dest = target.remote_mod_dir(remote_mod_id)

    adb.shell(f"rm -rf {tmp}", check=False)
    adb.run("push", str(local_dir).replace("\\", "/"), tmp, check=True)

    if target.use_run_as:
        script = (
            f"rm -rf {remote_dest} && "
            f"mkdir -p files/mods && "
            f"cp -r {tmp} {remote_dest}"
        )
        result = adb.shell(f"run-as {game_package} sh -c '{script}'", check=False)
        if result.returncode != 0:
            err = (result.stderr or result.stdout or "").strip()
            _fail(
                f"Failed to copy {remote_mod_id} into app sandbox.\n"
                f"  Is USB debugging authorized? Is {game_package} installed?\n"
                f"  adb: {err}"
            )
        listing = adb.shell(
            f"run-as {game_package} ls -la {remote_dest}/",
            check=False,
        ).stdout.strip()
    else:
        adb.shell(f"mkdir -p {LAUNCHER_MODS_ROOT}", check=False)
        result = adb.shell(f"rm -rf {remote_dest} && mv {tmp} {remote_dest}", check=False)
        if result.returncode != 0:
            err = (result.stderr or result.stdout or "").strip()
            _fail(f"Failed to install {remote_mod_id} under {LAUNCHER_MODS_ROOT}.\n  adb: {err}")
        listing = adb.shell(f"ls -la {remote_dest}/", check=False).stdout.strip()

    adb.shell(f"rm -rf {tmp}", check=False)
    print(f"Pushed {remote_mod_id}:")
    for line in listing.splitlines():
        print(f"  {line}")


def _restart_app(adb: Adb, package: str) -> None:
    adb.shell(f"am force-stop {package}", check=False)
    adb.shell(
        f"monkey -p {package} -c android.intent.category.LAUNCHER 1",
        check=False,
    )
    print(f"Restarted {package}")


def main() -> int:
    ap = argparse.ArgumentParser(
        description=(
            "Push build/KitLib to Android. Default target is StS2 Launcher "
            f"({LAUNCHER_MODS_ROOT}/)."
        ),
    )
    ap.add_argument(
        "--target",
        choices=tuple(PUSH_TARGETS),
        default=os.environ.get("ANDROID_PUSH_TARGET", "launcher").strip() or "launcher",
        help=(
            "launcher = StS2LauncherMM/Mods + restart launcher (default); "
            "game = com.megacrit.sts2 files/mods sandbox + restart game directly"
        ),
    )
    ap.add_argument(
        "--package",
        default=DEFAULT_GAME_PACKAGE,
        help=f"Game app id for --target game (default: {DEFAULT_GAME_PACKAGE})",
    )
    ap.add_argument(
        "-s",
        "--serial",
        default=os.environ.get("ANDROID_SERIAL", "").strip() or None,
        help="adb device serial (default: single connected device)",
    )
    ap.add_argument(
        "--skip-ritsulib",
        action="store_true",
        help="Do not push STS2-RitsuLib from NuGet cache",
    )
    ap.add_argument(
        "--no-build",
        action="store_true",
        help="Do not run make build; use existing build/ artifacts",
    )
    ap.add_argument(
        "--restart",
        action="store_true",
        help="Force-stop and relaunch the target app after push",
    )
    args = ap.parse_args()

    repo_root = _REPO_ROOT.resolve()
    push_target = _resolve_push_target(args.target)
    adb_exe = _resolve_adb()
    adb = Adb(adb_exe, args.serial)

    devices = _list_devices(adb)
    if not devices:
        _fail("No authorized adb device. Connect phone, enable USB debugging, accept the prompt.")
    if args.serial is None and len(devices) > 1:
        _fail("Multiple devices connected; pass --serial.\n" + "\n".join(f"  {d}" for d in devices))
    if args.serial is None:
        adb.serial = devices[0]

    restart_package = args.package if push_target.use_run_as else push_target.restart_package

    print(f"adb: {adb_exe}")
    print(f"device: {adb.serial}")
    print(f"target: {push_target.name}")
    print(f"mods root: {push_target.mods_root_display}")
    if push_target.use_run_as:
        print(f"game package: {args.package}")

    if not args.no_build:
        make = "make" if shutil.which("make") else None
        if not make:
            _fail("`make` not found; run `make build` manually or pass --no-build.")
        print("Building (build)...")
        subprocess.run([make, "build"], cwd=repo_root, check=True)

    staging_dirs: list[Path] = []
    try:
        mods: list[tuple[Path, str]] = []
        if not args.skip_ritsulib:
            ritsu = _stage_ritsulib(repo_root)
            if ritsu is None:
                print(
                    "Warning: STS2-RitsuLib not found in NuGet cache; skipping.\n"
                    "  Tip: run `dotnet restore KitLib.sln` first.",
                    file=sys.stderr,
                )
            else:
                staging_dirs.append(ritsu)
                mods.append((ritsu, RITSU_MOD_ID))

        kitlib_build = _validate_kitlib_build(repo_root)
        kitlib = _stage_tree(kitlib_build, prefix="sts2-push-kitlib-")
        staging_dirs.append(kitlib)
        mods.append((kitlib, KITLIB_MOD_ID))

        for local_dir, mod_id in mods:
            _push_mod_dir(adb, push_target, args.package, local_dir, mod_id)

        if args.restart:
            _restart_app(adb, restart_package)

        if push_target.name == "launcher":
            print("Done. Open StS2 Launcher and start the game from there.")
        else:
            print("Done. Fully quit and reopen the game if mods do not appear.")
    finally:
        for d in staging_dirs:
            shutil.rmtree(d, ignore_errors=True)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
